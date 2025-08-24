// FILE: OneBitRob/AI/SpellDecisionSystem.cs
// CHANGES:
// - Drive selection by SpellKind & SpellAcquireMode.
// - SingleTarget kinds: select entity. AoE kinds: select position.
// - Always consume requests.
// - FIX: renamed 'ents' variables to avoid conflicts.
// - FIX: EffectOverTimeTarget and EffectOverTimeArea now actually produce CastRequests.
// - FIX: DesiredDestination nudge safely adds component if missing.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob;
using OneBitRob.ECS;
using UnityEngine;

namespace OneBitRob.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateBefore(typeof(SpellExecutionSystem))]
    [UpdateAfter(typeof(CastSpellSystem))]   
    public partial struct SpellDecisionSystem : ISystem
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;
        ComponentLookup<HealthMirror> _hpRO;

        EntityQuery _q;

        public void OnCreate(ref SystemState state)
        {
            _posRO = state.GetComponentLookup<LocalTransform>(true);
            _factRO = state.GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            _hpRO = state.GetComponentLookup<HealthMirror>(true);

            _q = state.GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<SpellConfig>(),
                        ComponentType.ReadWrite<SpellDecisionRequest>(),
                        ComponentType.ReadWrite<CastRequest>(),
                        ComponentType.ReadOnly<SpellWindup>(),
                        ComponentType.ReadOnly<SpellCooldown>()
                    }
                }
            );

            state.RequireForUpdate(_q);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            _factRO.Update(ref state);
            _hpRO.Update(ref state);

            var em = state.EntityManager;
            var spellEntities = _q.ToEntityArray(Allocator.Temp);
            var now = (float)SystemAPI.Time.ElapsedTime;

            for (int i = 0; i < spellEntities.Length; i++)
            {
                var e = spellEntities[i];

                var decide = em.GetComponentData<SpellDecisionRequest>(e);
                if (decide.HasValue == 0) continue;

                var wind = em.GetComponentData<SpellWindup>(e);
                var cool = em.GetComponentData<SpellCooldown>(e);

                // Can't decide while winding up or cooling down
                if (wind.Active != 0 || now < cool.NextTime)
                {
                    decide.HasValue = 0;
                    em.SetComponentData(e, decide);
                    continue;
                }

                var cfg = em.GetComponentData<SpellConfig>(e);
                var cast = em.GetComponentData<CastRequest>(e);
                cast.HasValue = 0;
                cast.Kind = CastKind.None;
                cast.Target = Entity.Null;
                cast.AoEPosition = float3.zero;

                switch (cfg.Kind)
                {
                    // ───────────────────────────────────────────────── Single-target kinds
                    case SpellKind.ProjectileLine:
                    case SpellKind.Chain:
                    {
                        var tgt = SelectSingleTarget(e, cfg);
                        if (tgt != Entity.Null)
                        {
                            cast.Kind = CastKind.SingleTarget;
                            cast.Target = tgt;
                            cast.HasValue = 1;
                        }
                        break;
                    }

                    case SpellKind.EffectOverTimeTarget:
                    {
                        // First, try to actually select a target to cast on
                        var tgt = SelectSingleTarget(e, cfg);
                        if (tgt != Entity.Null)
                        {
                            cast.Kind = CastKind.SingleTarget;
                            cast.Target = tgt;
                            cast.HasValue = 1;
                        }
                        else
                        {
                            // If we failed (common when out of range), nudge toward lowest-health ally when applicable
                            if (cfg.AcquireMode == SpellAcquireMode.LowestHealthAlly)
                            {
                                var approach = new LowestHealthAllyTargeting().GetTarget(self: e, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                                if (approach != Entity.Null && _posRO.HasComponent(approach))
                                {
                                    var dd = new DesiredDestination { Position = _posRO[approach].Position, HasValue = 1 };
                                    if (em.HasComponent<DesiredDestination>(e)) em.SetComponentData(e, dd);
                                    else em.AddComponentData(e, dd);
                                }
                            }
                        }
                        break;
                    }

                    // ───────────────────────────────────────────────── AoE kinds
                    case SpellKind.EffectOverTimeArea:
                    {
                        // Try to choose an AoE point first
                        if (TrySelectAoE(e, cfg, out var point))
                        {
                            cast.Kind = CastKind.AreaOfEffect;
                            cast.AoEPosition = point;
                            cast.HasValue = 1;
                        }
                        else
                        {
                            // Else nudge toward densest enemy cluster
                            if (cfg.AcquireMode == SpellAcquireMode.DensestEnemyCluster && _posRO.HasComponent(e))
                            {
                                var selfPos = _posRO[e].Position;

                                byte enemyFaction =
                                    (_factRO.HasComponent(e) && _factRO[e].Faction == OneBitRob.Constants.GameConstants.ENEMY_FACTION)
                                        ? OneBitRob.Constants.GameConstants.ALLY_FACTION
                                        : OneBitRob.Constants.GameConstants.ENEMY_FACTION;

                                var wanted = default(FixedList128Bytes<byte>);
                                wanted.Add(enemyFaction);

                                using var candidates = new NativeList<Entity>(Allocator.Temp);
                                OneBitRob.ECS.SpatialHashSearch.CollectInSphere(selfPos, cfg.Range * 1.5f, wanted, candidates, ref _posRO, ref _factRO);

                                float3 best = selfPos;
                                float bestCount = 0;
                                float r2 = math.max(0.1f, cfg.AreaRadius) * 2f;

                                for (int ci = 0; ci < candidates.Length; ci++)
                                {
                                    var cpos = _posRO[candidates[ci]].Position;
                                    int count = 0;
                                    for (int cj = 0; cj < candidates.Length; cj++)
                                        if (math.distance(cpos, _posRO[candidates[cj]].Position) <= r2)
                                            count++;

                                    if (count > bestCount)
                                    {
                                        bestCount = count;
                                        best = cpos;
                                    }
                                }

                                var dd = new DesiredDestination { Position = best, HasValue = 1 };
                                if (em.HasComponent<DesiredDestination>(e)) em.SetComponentData(e, dd);
                                else em.AddComponentData(e, dd);
                            }
                        }
                        break;
                    }

                    case SpellKind.Summon:
                    {
                        // Summon at self (simple baseline)
                        if (_posRO.HasComponent(e))
                        {
                            cast.Kind = CastKind.AreaOfEffect;
                            cast.AoEPosition = _posRO[e].Position;
                            cast.HasValue = 1;
                        }
                        break;
                    }
                }

                em.SetComponentData(e, cast);

                // Always consume the decision request this frame
                decide.HasValue = 0;
                em.SetComponentData(e, decide);
            }

            spellEntities.Dispose();
        }

        [BurstCompile]
        private Entity SelectSingleTarget(Entity self, in SpellConfig cfg)
        {
            switch (cfg.AcquireMode)
            {
                case SpellAcquireMode.LowestHealthAlly:
                    return new LowestHealthAllyTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);

                case SpellAcquireMode.DensestEnemyCluster: // fall through to closest for single-target
                case SpellAcquireMode.ClosestEnemy:
                default:
                    return new ClosestEnemySpellTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
            }
        }

        [BurstCompile]
        private bool TrySelectAoE(Entity self, in SpellConfig cfg, out float3 point)
        {
            switch (cfg.AcquireMode)
            {
                case SpellAcquireMode.DensestEnemyCluster:
                    return new DensestEnemyClusterTargeting().TryGetAOETargetPoint(self, in cfg, ref _posRO, ref _factRO, out point);

                case SpellAcquireMode.LowestHealthAlly:
                case SpellAcquireMode.ClosestEnemy:
                default:
                    var tgt = new ClosestEnemySpellTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                    if (tgt != Entity.Null && _posRO.HasComponent(tgt))
                    {
                        point = _posRO[tgt].Position;
                        return true;
                    }

                    if (_posRO.HasComponent(self))
                    {
                        point = _posRO[self].Position;
                        return true;
                    } // fallback

                    point = default;
                    return false;
            }
        }
    }
}
