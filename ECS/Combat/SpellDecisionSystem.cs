// FILE: OneBitRob/AI/SpellDecisionSystem.cs
// Change applied: removed UpdateAfter(CastSpellSystem); kept UpdateBefore(CastExecutionSystem).

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateBefore(typeof(CastExecutionSystem))]   // ensure cast sees our CastRequest
    public partial struct SpellDecisionSystem : ISystem
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;
        ComponentLookup<HealthMirror> _hpRO;

        EntityQuery _q;

        public void OnCreate(ref SystemState state)
        {
            _posRO  = state.GetComponentLookup<LocalTransform>(true);
            _factRO = state.GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            _hpRO   = state.GetComponentLookup<HealthMirror>(true);

            _q = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<SpellConfig>(),
                    ComponentType.ReadWrite<SpellDecisionRequest>(),
                    ComponentType.ReadWrite<CastRequest>(),
                }
            });

            state.RequireForUpdate(_q);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            _factRO.Update(ref state);
            _hpRO.Update(ref state);

            var em   = state.EntityManager;
            var ents = _q.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];

                var decide = em.GetComponentData<SpellDecisionRequest>(e);
                if (decide.HasValue == 0) continue; // no BT request this frame → skip (1‑frame latency is fine)

                var cfg = em.GetComponentData<SpellConfig>(e);
                var cast = em.GetComponentData<CastRequest>(e);
                cast.HasValue = 0;
                cast.Kind = CastKind.None;
                cast.Target = Entity.Null;
                cast.AoEPosition = float3.zero;

                switch (cfg.TargetType)
                {
                    case SpellTargetType.SingleTarget:
                    case SpellTargetType.MultiTarget: // fallback to single target for now
                    {
                        Entity tgt = SelectSingleTarget(e, cfg);
                        if (tgt != Entity.Null)
                        {
                            cast.Kind   = CastKind.SingleTarget;
                            cast.Target = tgt;
                            cast.HasValue = 1;
                        }
                        break;
                    }

                    case SpellTargetType.AreaOfEffect:
                    {
                        if (TrySelectAoE(e, cfg, out var point))
                        {
                            cast.Kind = CastKind.AreaOfEffect;
                            cast.AoEPosition = point;
                            cast.HasValue = 1;
                        }
                        break;
                    }
                }

                em.SetComponentData(e, cast);
                decide.HasValue = 0; // consume BT request
                em.SetComponentData(e, decide);
            }

            ents.Dispose();
        }

        [BurstCompile]
        private Entity SelectSingleTarget(Entity self, in SpellConfig cfg)
        {
            switch (cfg.Strategy)
            {
                case SpellTargetingStrategyType.LowestHealthAlly:
                    return new LowestHealthAllyTargeting()
                        .GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);

                case SpellTargetingStrategyType.ClosestEnemy:
                default:
                    return new ClosestEnemySpellTargeting()
                        .GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
            }
        }

        [BurstCompile]
        private bool TrySelectAoE(Entity self, in SpellConfig cfg, out float3 point)
        {
            switch (cfg.Strategy)
            {
                case SpellTargetingStrategyType.DensestCluster:
                    return new DensestEnemyClusterTargeting()
                        .TryGetAOETargetPoint(self, in cfg, ref _posRO, ref _factRO, out point);

                case SpellTargetingStrategyType.ClosestEnemy:
                default:
                    var tgt = new ClosestEnemySpellTargeting()
                        .GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                    if (tgt != Entity.Null && _posRO.HasComponent(tgt))
                    {
                        point = _posRO[tgt].Position;
                        return true;
                    }
                    point = default;
                    return false;
            }
        }
    }
}
