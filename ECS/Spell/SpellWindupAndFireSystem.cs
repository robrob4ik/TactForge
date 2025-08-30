
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using OneBitRob.FX; 

namespace OneBitRob.AI
{
    /// <summary>
    /// Spell pipeline: consumes CastRequest, starts windup (with optional delay),
    /// faces the aim point, and fires when windup releases (spawns projectiles / DoT / Chain / Summon).
    /// Also sets cooldown.
    /// </summary>
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct SpellWindupAndFireSystem : ISystem
    {
        private EntityQuery _castQuery;
        private EntityQuery _pendingQuery;
        private ComponentLookup<LocalTransform> _posRO;
        private ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

        public void OnCreate(ref SystemState state)
        {
            _castQuery    = state.GetEntityQuery(ComponentType.ReadWrite<CastRequest>(), ComponentType.ReadOnly<SpellConfig>());
            _pendingQuery = state.GetEntityQuery(ComponentType.ReadWrite<SpellWindup>(), ComponentType.ReadOnly<SpellConfig>());
            _posRO        = state.GetComponentLookup<LocalTransform>(true);
            _factRO       = state.GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            state.RequireForUpdate(_castQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            _factRO.Update(ref state);

            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            // Release pending casts when windup timer finishes
            var wents = _pendingQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < wents.Length; i++)
            {
                var e = wents[i];
                var w = em.GetComponentData<SpellWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                var cfg = em.GetComponentData<SpellConfig>(e);
                FireSpell(ref state, ref ecb, e, in cfg, in w); // Fire & feedbacks

                if (em.HasComponent<SpellCooldown>(e))
                {
                    var cd = em.GetComponentData<SpellCooldown>(e);
                    cd.NextTime = now + max(0f, cfg.Cooldown);
                    ecb.SetComponent(e, cd);
                }

                w.Active = 0;
                ecb.SetComponent(e, w);
            }
            wents.Dispose();

            // Start new windups / set facing / play cast anims / consume CastRequest
            var ents = _castQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var req = em.GetComponentData<CastRequest>(e);
                if (req.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                var cfg   = em.GetComponentData<SpellConfig>(e);

                var w = em.HasComponent<SpellWindup>(e) ? em.GetComponentData<SpellWindup>(e) : default;
                w.Active        = 1;
                w.ReleaseTime   = now + max(0f, cfg.CastTime);
                w.FacingDeadline = w.ReleaseTime;

                float3 aimPos = float3.zero;
                switch (req.Kind)
                {
                    case CastKind.SingleTarget:
                        w.HasAimPoint = 0;
                        w.AimTarget   = req.Target;
                        if (_posRO.HasComponent(req.Target)) aimPos = _posRO[req.Target].Position;

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
                        w.Active = 0;
                        break;
                }

                if (w.Active != 0)
                {
                    // Face aim pos immediately
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

                    // Trigger spell animation if any
                    if (brain != null && brain.UnitDefinition != null)
                    {
                        var spells = brain.UnitDefinition.unitSpells;
                        if (spells != null && spells.Count > 0 && spells[0] != null)
                        {
                            var sd = spells[0];
                            brain.CombatSubsystem?.PlaySpell(sd.animations);

                            // NEW: prepare feedback at caster when windup starts
                            float3 casterPos = _posRO.HasComponent(e) ? _posRO[e].Position : float3.zero;
                            FeedbackService.TryPlay(sd.prepareFeedback, brain.transform, (UnityEngine.Vector3)casterPos);
                        }
                    }

                    if (em.HasComponent<SpellWindup>(e)) ecb.SetComponent(e, w);
                    else                                ecb.AddComponent(e, w);
                }

                // consume request
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

            float3 fwd   = new float3(0,0,1);
            float3 up    = new float3(0,1,0);
            float3 right = new float3(1,0,0);
            if (_posRO.HasComponent(e))
            {
                var rot = _posRO[e].Rotation;
                fwd   = normalizesafe(mul(rot, new float3(0,0,1)));
                up    = normalizesafe(mul(rot, new float3(0,1,0)));
                right = normalizesafe(mul(rot, new float3(1,0,0)));
            }

            var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

            float3 aimPos = w.HasAimPoint != 0
                ? w.AimPoint
                : (w.AimTarget != Entity.Null && _posRO.HasComponent(w.AimTarget) ? _posRO[w.AimTarget].Position : selfPos);

            switch (cfg.Kind)
            {
                case SpellKind.ProjectileLine:
                {
                    float3 origin = selfPos
                        + fwd   * max(0f, cfg.MuzzleForward)
                        + right * cfg.MuzzleLocalOffset.x
                        + up    * cfg.MuzzleLocalOffset.y
                        + fwd   * cfg.MuzzleLocalOffset.z;

                    float3 dir = normalizesafe(aimPos - origin, fwd); dir.y = 0;

                    int mask = ~0;
                    if (brain != null)
                        mask = cfg.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    float radius = max(0f, cfg.ProjectileRadius * max(0.0001f, stats.ProjectileRadiusMult));

                    var req = new SpellProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = dir,
                        Speed       = max(0.01f, cfg.ProjectileSpeed),
                        Damage      = cfg.EffectType == SpellEffectType.Negative ? max(0f, cfg.Amount) : -max(0f, cfg.Amount),
                        MaxDistance = max(0.1f, cfg.ProjectileMaxDistance),
                        Radius      = radius,
                        ProjectileIdHash = cfg.ProjectileIdHash,
                        LayerMask   = mask,
                        Pierce      = 1,
                        HasValue    = 1
                    };
                    if (em.HasComponent<SpellProjectileSpawnRequest>(e)) ecb.SetComponent(e, req);
                    else                                                                ecb.AddComponent(e, req);
                    break;
                }

                case SpellKind.EffectOverTimeArea:
                {
                    int mask = ~0;
                    if (brain != null)
                        mask = cfg.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    float areaRadius = cfg.AreaRadius * max(0.0001f, stats.SpellAoeMult);

                    var area = new DoTArea
                    {
                        Position       = w.HasAimPoint != 0 ? w.AimPoint : (_posRO.HasComponent(e) ? _posRO[e].Position : float3.zero),
                        Radius         = max(0f, areaRadius),
                        AmountPerTick  = max(0f, cfg.Amount),
                        Interval       = max(0.05f, cfg.TickInterval),
                        Remaining      = max(0f, cfg.Duration),
                        NextTick       = 0f,
                        Positive       = (byte)(cfg.EffectType == SpellEffectType.Positive ? 1 : 0),
                        AreaVfxIdHash  = cfg.AreaVfxIdHash,
                        LayerMask      = mask,
                        VfxYOffset     = max(0f, cfg.AreaVfxYOffset)
                    };
                    if (em.HasComponent<DoTArea>(e)) ecb.SetComponent(e, area);
                    else                                           ecb.AddComponent(e, area);

                    if (brain != null)
                    {
                        var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                        if (spells != null && spells.Count > 0 && spells[0] != null)
                        {
                            var sd = spells[0];
                            FeedbackService.TryPlay(sd.impactFeedback, null, (UnityEngine.Vector3)aimPos);
                        }
                    }
                    break;
                }

                case SpellKind.Chain:
                {
                    float3 origin = selfPos
                                    + fwd   * max(0f, cfg.MuzzleForward)
                                    + right * cfg.MuzzleLocalOffset.x
                                    + up    * cfg.MuzzleLocalOffset.y
                                    + fwd   * cfg.MuzzleLocalOffset.z;

                    var runner = new SpellChainRunner
                    {
                        Remaining        = max(1, cfg.ChainMaxTargets),
                        Radius           = max(0f, cfg.ChainRadius /* można też skalać AOE tu jeśli chcesz */),
                        JumpDelay        = max(0f, cfg.ChainJumpDelay),
                        ProjectileSpeed  = max(0.01f, cfg.ProjectileSpeed),
                        Amount           = max(0f, cfg.Amount),
                        Positive         = (byte)(cfg.EffectType == SpellEffectType.Positive ? 1 : 0),
                        ProjectileIdHash = cfg.ProjectileIdHash,
                        FromPos          = origin,
                        HasFromPos       = 1,
                        CurrentTarget    = w.AimTarget,
                        PreviousTarget   = Unity.Entities.Entity.Null,
                        Caster           = e,
                        CasterFaction    = SystemAPI.HasComponent<SpatialHashComponents.SpatialHashTarget>(e)
                            ? SystemAPI.GetComponent<SpatialHashComponents.SpatialHashTarget>(e).Faction
                            : (byte)OneBitRob.Constants.GameConstants.ALLY_FACTION,
                        LayerMask        = (brain != null
                            ? (cfg.EffectType == SpellEffectType.Positive
                                ? brain.GetFriendlyLayerMask().value
                                : brain.GetDamageableLayerMask().value)
                            : ~0)
                    };

                    if (em.HasComponent<SpellChainRunner>(e)) ecb.SetComponent(e, runner);
                    else                                                   ecb.AddComponent(e, runner);
                    break;
                }

                case SpellKind.Summon:
                {
                    var summon = new SummonRequest
                    {
                        PrefabIdHash = cfg.SummonPrefabHash,
                        Position     = aimPos,
                        Count        = 1,
                        Faction      = SystemAPI.HasComponent<SpatialHashComponents.SpatialHashTarget>(e)
                            ? SystemAPI.GetComponent<SpatialHashComponents.SpatialHashTarget>(e).Faction
                            : (byte)0,
                        HasValue     = (cfg.SummonPrefabHash != 0 ? 1 : 0)
                    };

                    if (summon.HasValue != 0)
                    {
                        if (em.HasComponent<SummonRequest>(e)) ecb.SetComponent(e, summon);
                        else                                   ecb.AddComponent(e, summon);
                    }
                    break;
                }
            }

            if (brain != null)
            {
                var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                if (spells != null && spells.Count > 0 && spells[0] != null)
                {
                    var sd = spells[0];
                    FeedbackService.TryPlay(sd.fireFeedback, brain.transform, (UnityEngine.Vector3)selfPos);
                }
            }
        }
    }
}
