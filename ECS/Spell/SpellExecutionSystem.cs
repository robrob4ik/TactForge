// CHANGED: masks, chain as runner, approach nudges

using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellDecisionSystem))]
    public partial struct SpellExecutionSystem : ISystem
    {
        private EntityQuery _castQuery;
        private EntityQuery _pendingQuery;
        private ComponentLookup<LocalTransform> _posRO;
        private ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

        public void OnCreate(ref SystemState state)
        {
            _castQuery   = state.GetEntityQuery(ComponentType.ReadWrite<CastRequest>(), ComponentType.ReadOnly<SpellConfig>());
            _pendingQuery= state.GetEntityQuery(ComponentType.ReadWrite<SpellWindup>(), ComponentType.ReadOnly<SpellConfig>());
            _posRO       = state.GetComponentLookup<LocalTransform>(true);
            _factRO      = state.GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            state.RequireForUpdate(_castQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            _factRO.Update(ref state);

            var em  = state.EntityManager;
            var now = (float)SystemAPI.Time.ElapsedTime;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Release pending casts
            var wents = _pendingQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < wents.Length; i++)
            {
                var e = wents[i];
                var w = em.GetComponentData<SpellWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                var cfg = em.HasComponent<SpellConfig>(e) ? em.GetComponentData<SpellConfig>(e) : default;

                FireSpell(ref state, ref ecb, e, in cfg, in w);

                // cooldown
                if (em.HasComponent<SpellCooldown>(e))
                {
                    var cd = em.GetComponentData<SpellCooldown>(e);
                    cd.NextTime = now + math.max(0f, cfg.Cooldown);
                    ecb.SetComponent(e, cd);
                }

                w.Active = 0;
                ecb.SetComponent(e, w);
            }
            wents.Dispose();

            // Commit new casts (schedule fire with delay)
            var ents = _castQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var req = em.GetComponentData<CastRequest>(e);
                if (req.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                var cfg   = em.GetComponentData<SpellConfig>(e);

                // Prepare windup
                var w = em.HasComponent<SpellWindup>(e) ? em.GetComponentData<SpellWindup>(e) : default;
                w.Active        = 1;
                w.ReleaseTime   = (float)SystemAPI.Time.ElapsedTime + math.max(0f, cfg.CastTime);
                w.FacingDeadline = w.ReleaseTime;

                float3 aimPos = float3.zero;
                switch (req.Kind)
                {
                    case CastKind.SingleTarget:
                        w.HasAimPoint = 0;
                        w.AimTarget   = req.Target;
                        if (_posRO.HasComponent(req.Target))
                            aimPos = _posRO[req.Target].Position;

                        if (brain != null)
                        {
                            brain.CurrentSpellTarget = UnitBrainRegistry.GetGameObject(req.Target);
                            brain.CurrentSpellTargets = null;
                            brain.CurrentSpellTargetPosition = null;
                        }
                        break;

                    case CastKind.AreaOfEffect:
                        w.HasAimPoint = 1;
                        w.AimPoint    = req.AoEPosition;
                        aimPos        = req.AoEPosition;

                        if (brain != null)
                        {
                            brain.CurrentSpellTarget = null;
                            brain.CurrentSpellTargets = null;
                            brain.CurrentSpellTargetPosition = req.AoEPosition;
                        }
                        break;

                    default:
                        // No valid cast — small approach nudge for heal/AoE when OOR:
                        // (handled in SpellDecisionSystem by setting DesiredDestination; kept here as no-op)
                        w.Active = 0;
                        break;
                }

                if (w.Active != 0)
                {
                    if (em.HasComponent<DesiredFacing>(e))
                    {
                        var df = em.GetComponentData<DesiredFacing>(e);
                        df.TargetPosition = aimPos;
                        df.HasValue = 1;
                        ecb.SetComponent(e, df);
                    }
                    else
                    {
                        ecb.AddComponent(e, new DesiredFacing { TargetPosition = aimPos, HasValue = 1 });
                    }

                    if (brain != null && brain.UnitDefinition != null)
                    {
                        var spells = brain.UnitDefinition.unitSpells;
                        if (spells != null && spells.Count > 0 && spells[0] != null)
                            brain.CombatSubsystem?.PlaySpell(spells[0].animations);
                    }

                    if (em.HasComponent<SpellWindup>(e)) ecb.SetComponent(e, w);
                    else                                ecb.AddComponent(e, w);
                }

                // consume
                req.HasValue = 0;
                ecb.SetComponent(e, req);
            }
            ents.Dispose();

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void FireSpell(ref SystemState state, ref EntityCommandBuffer ecb, Entity e, in SpellConfig cfg, in SpellWindup w)
        {
            var em = state.EntityManager;
            var brain = UnitBrainRegistry.Get(e);

            float3 selfPos = _posRO.HasComponent(e) ? _posRO[e].Position : float3.zero;

            float3 fwd = new float3(0,0,1);
            float3 up  = new float3(0,1,0);
            float3 right = new float3(1,0,0);
            if (_posRO.HasComponent(e))
            {
                var rot = _posRO[e].Rotation;
                fwd   = math.normalizesafe(math.mul(rot, new float3(0,0,1)));
                up    = math.normalizesafe(math.mul(rot, new float3(0,1,0)));
                right = math.normalizesafe(math.mul(rot, new float3(1,0,0)));
            }

            float3 aimPos = w.HasAimPoint != 0
                ? w.AimPoint
                : (w.AimTarget != Entity.Null && _posRO.HasComponent(w.AimTarget) ? _posRO[w.AimTarget].Position : selfPos);

            switch (cfg.Kind)
            {
                case SpellKind.ProjectileLine:
                {
                    float3 origin = selfPos
                        + fwd   * math.max(0f, cfg.MuzzleForward)
                        + right * cfg.MuzzleLocalOffset.x
                        + up    * cfg.MuzzleLocalOffset.y
                        + fwd   * cfg.MuzzleLocalOffset.z;

                    float3 dir = math.normalizesafe(aimPos - origin, fwd);
                    dir.y = 0;

                    int mask = ~0;
                    if (brain != null)
                        mask = cfg.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    var req = new SpellProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = dir,
                        Speed       = math.max(0.01f, cfg.ProjectileSpeed),
                        Damage      = cfg.EffectType == SpellEffectType.Negative ? math.max(0f, cfg.Amount) : -math.max(0f, cfg.Amount),
                        MaxDistance = math.max(0.1f, cfg.ProjectileMaxDistance),
                        Radius      = math.max(0f, cfg.ProjectileRadius),
                        ProjectileIdHash = cfg.ProjectileIdHash,
                        LayerMask   = mask,
                        Pierce      = 1, // line spells pierce by default
                        HasValue    = 1
                    };
                    if (em.HasComponent<SpellProjectileSpawnRequest>(e)) ecb.SetComponent(e, req);
                    else                                                ecb.AddComponent(e, req);
                    break;
                }

                case SpellKind.EffectOverTimeTarget:
                {
                    if (w.AimTarget == Entity.Null) break;
                    var dot = new DotOnTarget
                    {
                        Target         = w.AimTarget,
                        AmountPerTick  = math.max(0f, cfg.Amount),
                        Interval       = math.max(0.05f, cfg.TickInterval),
                        Remaining      = math.max(0f, cfg.Duration),
                        NextTick       = 0f,
                        Positive       = (byte)(cfg.EffectType == SpellEffectType.Positive ? 1 : 0),
                        EffectVfxIdHash= cfg.EffectVfxIdHash
                    };
                    if (em.HasComponent<DotOnTarget>(e)) ecb.SetComponent(e, dot);
                    else                                  ecb.AddComponent(e, dot);
                    break;
                }

                case SpellKind.EffectOverTimeArea:
                {
                    int mask = ~0;
                    if (brain != null)
                        mask = cfg.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    var area = new DoTArea
                    {
                        Position       = aimPos,
                        Radius         = math.max(0f, cfg.AreaRadius),
                        AmountPerTick  = math.max(0f, cfg.Amount),
                        Interval       = math.max(0.05f, cfg.TickInterval),
                        Remaining      = math.max(0f, cfg.Duration),
                        NextTick       = 0f,
                        Positive       = (byte)(cfg.EffectType == SpellEffectType.Positive ? 1 : 0),
                        AreaVfxIdHash  = cfg.AreaVfxIdHash,
                        LayerMask      = mask
                    };
                    if (em.HasComponent<DoTArea>(e)) ecb.SetComponent(e, area);
                    else                              ecb.AddComponent(e, area);
                    break;
                }

             
                case SpellKind.Chain:
                {
                    // Compute muzzle origin same as projectile line
                    float3 origin = selfPos
                                    + fwd   * math.max(0f, cfg.MuzzleForward)
                                    + right * cfg.MuzzleLocalOffset.x
                                    + up    * cfg.MuzzleLocalOffset.y
                                    + fwd   * cfg.MuzzleLocalOffset.z;

                    var runner = new SpellChainRunner
                    {
                        Remaining        = math.max(1, cfg.ChainMaxTargets),
                        Radius           = math.max(0f, cfg.ChainRadius),
                        JumpDelay        = math.max(0f, cfg.ChainJumpDelay),
                        ProjectileSpeed  = math.max(0.01f, cfg.ProjectileSpeed),
                        Amount           = math.max(0f, cfg.Amount),
                        Positive         = (byte)(cfg.EffectType == SpellEffectType.Positive ? 1 : 0),
                        ProjectileIdHash = cfg.ProjectileIdHash,
                        FromPos          = origin,    // ← muzzle origin
                        HasFromPos       = 1,
                        CurrentTarget    = w.AimTarget,
                        PreviousTarget   = Entity.Null,

                        // Identity/masks
                        Caster           = e,
                        CasterFaction    = _factRO.HasComponent(e) ? _factRO[e].Faction : (byte)OneBitRob.Constants.GameConstants.ALLY_FACTION,
                        LayerMask        = (brain != null
                            ? (cfg.EffectType == SpellEffectType.Positive
                                ? brain.GetFriendlyLayerMask().value
                                : brain.GetDamageableLayerMask().value)
                            : ~0)
                    };

                    if (em.HasComponent<SpellChainRunner>(e)) ecb.SetComponent(e, runner);
                    else                                      ecb.AddComponent(e, runner);
                    break;
                }

                case SpellKind.Summon:
                {
                    var summon = new SummonRequest
                    {
                        PrefabIdHash = cfg.SummonPrefabHash,
                        Position     = aimPos,
                        Count        = 1,
                        Faction      = _factRO.HasComponent(e) ? _factRO[e].Faction : (byte)0,
                        HasValue     = (byte)(cfg.SummonPrefabHash != 0 ? 1 : 0)
                    };

                    if (summon.HasValue != 0)
                    {
                        if (em.HasComponent<SummonRequest>(e)) ecb.SetComponent(e, summon);
                        else                                   ecb.AddComponent(e, summon);
                    }
                    break;
                }
            }
        }
    }
}
