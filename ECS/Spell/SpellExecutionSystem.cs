// FILE: OneBitRob/AI/CastExecutionSystem.cs
// CHANGES:
// - Stop calling brain.TryCastSpell(). All effects are executed here.
// - Implement 5 spell kinds.
// - Facing gate retained with tolerance.
// - Starts cooldown after fire.
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellDecisionSystem))]
    public partial struct SpellExecutionSystem : ISystem
    {
        private EntityQuery _castQuery;
        private EntityQuery _windupQuery;
        private ComponentLookup<LocalTransform> _posRO;
        private ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

        public void OnCreate(ref SystemState state)
        {
            _castQuery   = state.GetEntityQuery(ComponentType.ReadWrite<CastRequest>(), ComponentType.ReadOnly<SpellConfig>());
            _windupQuery = state.GetEntityQuery(ComponentType.ReadWrite<SpellWindup>());
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

            // 1) Release windups
            var wents = _windupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < wents.Length; i++)
            {
                var e = wents[i];
                var w = em.GetComponentData<SpellWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                var cfg = em.HasComponent<SpellConfig>(e) ? em.GetComponentData<SpellConfig>(e) : default;

                // Facing tolerance gate (optional)
                if (cfg.RequireFacing != 0)
                {
                    if (TryGetAimPoint(e, in w, out float3 aim))
                    {
                        if (_posRO.HasComponent(e))
                        {
                            var self = _posRO[e];
                            float3 fwd = math.normalizesafe(math.mul(self.Rotation, new float3(0, 0, 1)));
                            fwd.y = 0;
                            float3 to = aim - self.Position; to.y = 0;
                            float lenTo = math.length(to);
                            if (lenTo > 1e-3f)
                            {
                                to /= lenTo;
                                float dot = math.saturate(math.dot(fwd, to));
                                float angle = math.degrees(math.acos(dot));
                                if (angle > math.max(1f, cfg.FaceToleranceDeg) && now < w.FacingDeadline)
                                {
                                    var brain = UnitBrainRegistry.Get(e);
                                    brain?.SetForcedFacing((Vector3)aim);
                                    w.ReleaseTime = now + 0.033f;
                                    ecb.SetComponent(e, w);
                                    continue;
                                }
                            }
                        }
                    }
                }

                // ───────────────────────── FIRE ─────────────────────────
                ExecuteSpell(ref state, ref ecb, e, in cfg, in w);

                // Cooldown starts now
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

            // 2) Commit casts (start windup)
            var ents = _castQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var req = em.GetComponentData<CastRequest>(e);
                if (req.HasValue == 0) continue;

                var b   = UnitBrainRegistry.Get(e);
                var cfg = em.GetComponentData<SpellConfig>(e);

                var w = em.HasComponent<SpellWindup>(e) ? em.GetComponentData<SpellWindup>(e) : default;
                w.Active        = 1;
                w.ReleaseTime   = now + math.max(0f, cfg.CastTime);
                w.FacingDeadline = w.ReleaseTime + math.max(0f, cfg.MaxExtraFaceDelay);

                switch (req.Kind)
                {
                    case CastKind.SingleTarget:
                        w.HasAimPoint = 0;
                        w.AimTarget   = req.Target;
                        if (b != null)
                        {
                            b.CurrentSpellTarget = UnitBrainRegistry.GetGameObject(req.Target);
                            b.CurrentSpellTargets = null;
                            b.CurrentSpellTargetPosition = null;
                        }
                        break;

                    case CastKind.AreaOfEffect:
                        w.HasAimPoint = 1;
                        w.AimPoint    = req.AoEPosition;
                        if (b != null)
                        {
                            b.CurrentSpellTarget = null;
                            b.CurrentSpellTargets = null;
                            b.CurrentSpellTargetPosition = req.AoEPosition;
                        }
                        break;
                }

                if (b != null)
                {
                    b.RotateToSpellTarget();
                    b.CombatSubsystem?.PlaySpellPrepare(null);
                }

                if (em.HasComponent<SpellWindup>(e)) ecb.SetComponent(e, w);
                else                                ecb.AddComponent(e, w);

                req.HasValue = 0;
                ecb.SetComponent(e, req);
            }
            ents.Dispose();

            ecb.Playback(em);
            ecb.Dispose();
        }

        private bool TryGetAimPoint(Entity e, in SpellWindup w, out float3 aim)
        {
            if (w.HasAimPoint != 0) { aim = w.AimPoint; return true; }
            if (w.AimTarget != Entity.Null && _posRO.HasComponent(w.AimTarget))
            { aim = _posRO[w.AimTarget].Position; return true; }
            aim = default; return false;
        }

        private void ExecuteSpell(ref SystemState state, ref EntityCommandBuffer ecb, Entity e, in SpellConfig cfg, in SpellWindup w)
        {
            var em = state.EntityManager;
            var brain = UnitBrainRegistry.Get(e);

            // Try grab faction for summon
            byte faction = 0;
            if (state.EntityManager.HasComponent<SpatialHashComponents.SpatialHashTarget>(e))
                faction = state.EntityManager.GetComponentData<SpatialHashComponents.SpatialHashTarget>(e).Faction;

            float3 selfPos = _posRO.HasComponent(e) ? _posRO[e].Position : float3.zero;
            float3 aimPos = w.HasAimPoint != 0 ? w.AimPoint : (w.AimTarget != Entity.Null && _posRO.HasComponent(w.AimTarget) ? _posRO[w.AimTarget].Position : selfPos);
            float3 dir = math.normalizesafe(aimPos - selfPos, new float3(0,0,1));

            switch (cfg.Kind)
            {
                case SpellKind.ProjectileLine:
                {
                    var req = new SpellProjectileSpawnRequest
                    {
                        Origin      = selfPos,
                        Direction   = dir,
                        Speed       = math.max(0.01f, cfg.ProjectileSpeed),
                        Damage      = cfg.EffectType == SpellEffectType.Negative ? math.max(0f, cfg.Amount) : -math.max(0f, cfg.Amount),
                        MaxDistance = math.max(0.1f, cfg.ProjectileMaxDistance),
                        Radius      = math.max(0f, cfg.ProjectileRadius),
                        ProjectileIdHash = cfg.ProjectileIdHash,
                        LayerMask   = cfg.TargetLayerMask != 0 ? cfg.TargetLayerMask : (brain != null ? brain.GetDamageableLayerMask().value : ~0),
                        Pierce      = 1,
                        HasValue    = 1
                    };
                    if (em.HasComponent<SpellProjectileSpawnRequest>(e)) ecb.SetComponent(e, req);
                    else                                                ecb.AddComponent(e, req);
                    brain?.CombatSubsystem?.PlaySpellFire(null);
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
                    brain?.CombatSubsystem?.PlaySpellFire(null);
                    break;
                }

                case SpellKind.EffectOverTimeArea:
                {
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
                        LayerMask      = cfg.TargetLayerMask != 0 ? cfg.TargetLayerMask : (brain != null ? brain.GetDamageableLayerMask().value : ~0)
                    };
                    if (em.HasComponent<DoTArea>(e)) ecb.SetComponent(e, area);
                    else                              ecb.AddComponent(e, area);
                    brain?.CombatSubsystem?.PlaySpellFire(null);
                    break;
                }

                case SpellKind.Chain:
                {
                    // Greedy nearest-neighbor chain from target
                    int remaining = math.max(1, cfg.ChainMaxTargets);
                    float radius = math.max(0f, cfg.ChainRadius);
                    float delay = math.max(0f, cfg.ChainJumpDelay);

                    var visited = new NativeHashMap<Entity, byte>(remaining * 2, Allocator.Temp);
                    Entity current = w.AimTarget;

                    float now = (float)SystemAPI.Time.ElapsedTime;
                    float nextTime = now;

                    while (current != Entity.Null && remaining-- > 0)
                    {
                        if (!visited.TryAdd(current, 1)) break;

                        // Apply immediate (or you can buffer & schedule with delay if needed)
                        var targetBrain = UnitBrainRegistry.Get(current);
                        if (targetBrain != null && targetBrain.Health != null)
                        {
                            float amount = math.max(0f, cfg.Amount);
                            if (cfg.EffectType == SpellEffectType.Positive) amount = -amount; // negative damage -> heal
                            targetBrain.Health.Damage(amount, brain ? brain.gameObject : null, 0f, 0f, Vector3.zero);
                        }

                        // find next
                        current = FindNextChainTarget(current, radius, cfg, ref state);
                        if (delay > 0f) nextTime += delay; // TODO: if you want delayed chains, create a runner entity here
                    }

                    visited.Dispose();
                    brain?.CombatSubsystem?.PlaySpellFire(null);
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
                        brain?.CombatSubsystem?.PlaySpellFire(null);
                    }
                    break;
                }
            }
        }

        private Entity FindNextChainTarget(Entity from, float radius, in SpellConfig cfg, ref SystemState state)
        {
            // Collect in sphere and pick closest to 'from' that matches faction rule
            var posRO  = _posRO;
            var factRO = _factRO;

            if (!posRO.HasComponent(from)) return Entity.Null;

            var wanted = new Unity.Collections.FixedList128Bytes<byte>();
            // Positive => allies, Negative => enemies (assuming 2 factions)
            byte selfFaction = factRO[from].Faction;
            byte ally = selfFaction;
            byte enemy = (selfFaction == OneBitRob.Constants.GameConstants.ENEMY_FACTION)
                ? OneBitRob.Constants.GameConstants.ALLY_FACTION
                : OneBitRob.Constants.GameConstants.ENEMY_FACTION;

            if (cfg.EffectType == SpellEffectType.Positive) wanted.Add(ally);
            else wanted.Add(enemy);

            using var list = new NativeList<Entity>(Allocator.Temp);
            OneBitRob.ECS.SpatialHashSearch.CollectInSphere(
                posRO[from].Position, radius, wanted, list, ref posRO, ref factRO);

            Entity best = Entity.Null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < list.Length; i++)
            {
                var e = list[i];
                if (e == from) continue;
                float d = math.distance(posRO[e].Position, posRO[from].Position);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }
    }
}
