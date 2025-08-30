// Phase: PLAN — picks a cast (target or AoE point) and writes CastRequest.
// Renamed from SpellDecisionSystem for consistency with Plan → Aim → Execute.

// Notes:
// - Negative AoE: no self fallback. If no enemy point, no cast.
// - Positive AoE (heals/buffs): may fallback to self.

using OneBitRob.ECS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateBefore(typeof(CastSpellSystem))]        // Aim happens after plan
    [UpdateBefore(typeof(SpellWindupAndFireSystem))]  // Execute consumes the plan
    public partial struct SpellPlanSystem : ISystem
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
                });

            state.RequireForUpdate(_q);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            _factRO.Update(ref state);
            _hpRO.Update(ref state);

            var em  = state.EntityManager;
            var now = (float)SystemAPI.Time.ElapsedTime;
            var spellEntities = _q.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < spellEntities.Length; i++)
            {
                var e = spellEntities[i];

                var decide = em.GetComponentData<SpellDecisionRequest>(e);
                if (decide.HasValue == 0) continue;

                var wind = em.GetComponentData<SpellWindup>(e);
                var cool = em.GetComponentData<SpellCooldown>(e);

                // Can't plan while winding up or cooling down
                if (wind.Active != 0 || now < cool.NextTime)
                {
                    decide.HasValue = 0;
                    em.SetComponentData(e, decide);
                    continue;
                }

                var cfg  = em.GetComponentData<SpellConfig>(e);
                var cast = em.GetComponentData<CastRequest>(e);
                cast.HasValue   = 0;
                cast.Kind       = CastKind.None;
                cast.Target     = Entity.Null;
                cast.AoEPosition= float3.zero;

                var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                var cfgScaled = cfg;
                cfgScaled.Range = cfg.Range * math.max(0.0001f, stats.SpellRangeMult);
                
                switch (cfg.Kind)
                {
                    // Single-target
                    case SpellKind.ProjectileLine:
                    case SpellKind.Chain:
                    {
                        var tgt = SelectSingleTarget(e, cfg);
                        if (tgt != Entity.Null)
                        {
                            cast.Kind   = CastKind.SingleTarget;
                            cast.Target = tgt;
                            cast.HasValue = 1;
                        }
                        break;
                    }

                    case SpellKind.EffectOverTimeTarget:
                    {
                        var tgt = SelectSingleTarget(e, cfg);
                        if (tgt != Entity.Null)
                        {
                            cast.Kind   = CastKind.SingleTarget;
                            cast.Target = tgt;
                            cast.HasValue = 1;
                        }
                        break;
                    }

                    // AoE
                    case SpellKind.EffectOverTimeArea:
                    {
                        if (TrySelectAoE(e, cfg, out var point))
                        {
                            cast.Kind = CastKind.AreaOfEffect;
                            cast.AoEPosition = point;
                            cast.HasValue = 1;
                        }
                        break;
                    }

                    case SpellKind.Summon:
                    {
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
                decide.HasValue = 0; // always consume the request
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

                case SpellAcquireMode.DensestEnemyCluster: // single-target fallback → use closest enemy
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
                {
                    var tgt = new ClosestEnemySpellTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                    if (tgt != Entity.Null && _posRO.HasComponent(tgt))
                    {
                        point = _posRO[tgt].Position;
                        return true;
                    }

                    if (cfg.EffectType == SpellEffectType.Positive && _posRO.HasComponent(self))
                    {
                        point = _posRO[self].Position;
                        return true;
                    }

                    point = default;
                    return false;
                }
            }
        }
    }
}
