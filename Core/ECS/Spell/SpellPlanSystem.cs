// File: OneBitRob/AI/SpellPlanSystem.cs

using OneBitRob.ECS;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Transforms;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial struct SpellPlanSystem : ISystem
    {
        ComponentLookup<LocalTransform>    _posRO;
        ComponentLookup<SpatialHashTarget> _factRO;
        ComponentLookup<HealthMirror>      _hpRO;

        public void OnCreate(ref SystemState state)
        {
            _posRO  = state.GetComponentLookup<LocalTransform>(true);
            _factRO = state.GetComponentLookup<SpatialHashTarget>(true);
            _hpRO   = state.GetComponentLookup<HealthMirror>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            _factRO.Update(ref state);
            _hpRO.Update(ref state);

            var em  = state.EntityManager;
            float now = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (decideRW, e) in SystemAPI.Query<RefRW<SpellDecisionRequest>>()
                                                   .WithAll<SpellConfig, SpellWindup, SpellCooldown>()
                                                   .WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<SpellDecisionRequest>(e)) continue;

                var wind = em.GetComponentData<SpellWindup>(e);
                var cool = em.GetComponentData<SpellCooldown>(e);
                if (wind.Active != 0 || now < cool.NextTime)
                {
                    SystemAPI.SetComponentEnabled<SpellDecisionRequest>(e, false);
                    continue;
                }

                var cfg  = em.GetComponentData<SpellConfig>(e);
                var cast = em.HasComponent<CastRequest>(e) ? em.GetComponentData<CastRequest>(e) : default;

                var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;
                var cfgScaled = cfg; cfgScaled.Range = cfg.Range * max(0.0001f, stats.SpellRangeMult);

                cast.Kind        = CastKind.None;
                cast.Target      = Entity.Null;
                cast.AoEPosition = float3.zero;

                switch (cfg.Kind)
                {
                    case SpellKind.ProjectileLine:
                    case SpellKind.Chain:
                    case SpellKind.EffectOverTimeTarget:
                    {
                        var tgt = SelectSingleTarget(e, in cfgScaled);
                        if (tgt != Entity.Null)
                        {
                            cast.Kind   = CastKind.SingleTarget;
                            cast.Target = tgt;
                        }
                        break;
                    }

                    case SpellKind.EffectOverTimeArea:
                    {
                        if (TrySelectAoE(e, in cfgScaled, out var point))
                        {
                            cast.Kind        = CastKind.AreaOfEffect;
                            cast.AoEPosition = point;
                        }
                        break;
                    }

                    case SpellKind.Summon:
                    {
                        if (_posRO.HasComponent(e))
                        {
                            cast.Kind        = CastKind.AreaOfEffect;
                            cast.AoEPosition = _posRO[e].Position;
                        }
                        break;
                    }
                }

                if (cast.Kind != CastKind.None)
                {
                    if (!em.HasComponent<CastRequest>(e)) em.AddComponentData(e, cast);
                    else                                  em.SetComponentData(e, cast);

                    SystemAPI.SetComponentEnabled<CastRequest>(e, true);
                }

                SystemAPI.SetComponentEnabled<SpellDecisionRequest>(e, false);
            }
        }

        [BurstCompile]
        private Entity SelectSingleTarget(Entity self, in SpellConfig cfg)
        {
            switch (cfg.AcquireMode)
            {
                case SpellAcquireMode.LowestHealthAlly:
                    return new LowestHealthAllyTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);

                case SpellAcquireMode.DensestEnemyCluster:
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
                    if (tgt != Entity.Null && _posRO.HasComponent(tgt)) { point = _posRO[tgt].Position; return true; }

                    if (cfg.EffectType == SpellEffectType.Positive && _posRO.HasComponent(self))
                    { point = _posRO[self].Position; return true; }

                    point = default;
                    return false;
                }
            }
        }
    }
}
