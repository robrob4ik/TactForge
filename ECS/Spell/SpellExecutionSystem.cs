// FILE: OneBitRob/AI/CastExecutionSystem.cs
// CHANGES:
// - No “facing gate” (we still rotate, but non-blocking).
// - Use CastTime as Fire Delay after animation trigger.
// - Spell projectile origin uses MuzzleForward + MuzzleLocalOffset (same as ranged weapon).
// - Single-stage cast animation via CombatSubsystem.PlaySpell(AttackAnimationSet).

using OneBitRob;
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

            // 1) Release pending casts (fire moment)
            var wents = _pendingQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < wents.Length; i++)
            {
                var e = wents[i];
                var w = em.GetComponentData<SpellWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                var cfg = em.HasComponent<SpellConfig>(e) ? em.GetComponentData<SpellConfig>(e) : default;

                FireSpell(ref state, ref ecb, e, in cfg, in w);

                // Start cooldown
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

            // 2) Commit new casts (schedule fire with delay, rotate towards aim)
            var ents = _castQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var req = em.GetComponentData<CastRequest>(e);
                if (req.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                var cfg   = em.GetComponentData<SpellConfig>(e);

                // Determine aim
                var w = em.HasComponent<SpellWindup>(e) ? em.GetComponentData<SpellWindup>(e) : default;
                w.Active        = 1;
                w.ReleaseTime   = now + math.max(0f, cfg.CastTime); // Fire after delay
                w.FacingDeadline = w.ReleaseTime; // unused

                float3 aimPos = float3.zero;
                switch (req.Kind)
                {
                    case CastKind.SingleTarget:
                        w.HasAimPoint = 0;
                        w.AimTarget   = req.Target;

                        if (_posRO.HasComponent(req.Target))
                        {
                            aimPos = _posRO[req.Target].Position;
                        }

                        // Debug data for gizmos
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
                }

                // Push facing hint (unified with melee/ranged path via DesiredFacing + MonoBridge)
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

                // Trigger single-stage cast animation
                if (brain != null && brain.UnitDefinition != null)
                {
                    var spells = brain.UnitDefinition.unitSpells;
                    if (spells != null && spells.Count > 0 && spells[0] != null)
                    {
                        brain.CombatSubsystem?.PlaySpell(spells[0].animations);
                    }
                }

                if (em.HasComponent<SpellWindup>(e)) ecb.SetComponent(e, w);
                else                                ecb.AddComponent(e, w);

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

            // Compute forward/right/up from entity rotation
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
                    // Muzzle origin (same math as ranged weapons)
                    float3 origin = selfPos
                        + fwd   * math.max(0f, cfg.MuzzleForward)
                        + right * cfg.MuzzleLocalOffset.x
                        + up    * cfg.MuzzleLocalOffset.y
                        + fwd   * cfg.MuzzleLocalOffset.z;

                    float3 dir = math.normalizesafe(aimPos - origin, fwd);
                    dir.y = 0; // planar aim for topdown
#if UNITY_EDITOR
                    Debug.DrawRay((Vector3)origin, (Vector3)(dir * 1.25f), new Color(1f, 0.2f, 1f, 0.85f), 0.2f, false);
#endif

                    var req = new SpellProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = dir,
                        Speed       = math.max(0.01f, cfg.ProjectileSpeed),
                        Damage      = cfg.EffectType == SpellEffectType.Negative ? math.max(0f, cfg.Amount) : -math.max(0f, cfg.Amount),
                        MaxDistance = math.max(0.1f, cfg.ProjectileMaxDistance),
                        Radius      = math.max(0f, cfg.ProjectileRadius),
                        ProjectileIdHash = cfg.ProjectileIdHash,
                        LayerMask   = brain != null ? brain.GetDamageableLayerMask().value : ~0,
                        Pierce      = 1,
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
                        LayerMask      = brain != null ? brain.GetDamageableLayerMask().value : ~0
                    };
                    if (em.HasComponent<DoTArea>(e)) ecb.SetComponent(e, area);
                    else                              ecb.AddComponent(e, area);
                    break;
                }

                case SpellKind.Chain:
                {
                    // Simple greedy chain (instant). For delayed per-jump chains, queue a runner entity.
                    int remaining = math.max(1, cfg.ChainMaxTargets);
                    float radius = math.max(0f, cfg.ChainRadius);

                    var visited = new NativeHashMap<Entity, byte>(remaining * 2, Allocator.Temp);
                    Entity current = w.AimTarget;

                    while (current != Entity.Null && remaining-- > 0)
                    {
                        if (!visited.TryAdd(current, 1)) break;

                        var targetBrain = UnitBrainRegistry.Get(current);
                        if (targetBrain != null && targetBrain.Health != null)
                        {
                            float amount = math.max(0f, cfg.Amount);
                            if (cfg.EffectType == SpellEffectType.Positive) amount = -amount; // negative damage -> heal
                            targetBrain.Health.Damage(amount, brain ? brain.gameObject : null, 0f, 0f, Vector3.zero);
                        }

                        current = FindNextChainTarget(current, radius, cfg, ref state);
                    }

                    visited.Dispose();
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

        private Entity FindNextChainTarget(Entity from, float radius, in SpellConfig cfg, ref SystemState state)
        {
            var posRO  = _posRO;
            var factRO = _factRO;
            if (!posRO.HasComponent(from)) return Entity.Null;

            var wanted = new Unity.Collections.FixedList128Bytes<byte>();
            byte selfFaction = factRO[from].Faction;
            byte ally = selfFaction;
            byte enemy = (selfFaction == Constants.GameConstants.ENEMY_FACTION)
                ? Constants.GameConstants.ALLY_FACTION
                : Constants.GameConstants.ENEMY_FACTION;

            if (cfg.EffectType == SpellEffectType.Positive) wanted.Add(ally);
            else wanted.Add(enemy);

            using var list = new NativeList<Entity>(Allocator.Temp);
            SpatialHashSearch.CollectInSphere(
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
