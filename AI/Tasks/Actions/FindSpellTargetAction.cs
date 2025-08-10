// FILE: OneBitRob/AI/FindSpellTargetAction.cs
// Changes applied: replaced `out _` with a typed `float3 dummyPoint;`
// and added `using Unity.Mathematics;` to match the ECS strategy signature.

using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;   // <-- added

namespace OneBitRob.AI
{
    [NodeDescription("ECS-only check: is there a valid spell target now? (no Mono/GO writes)")]
    public class FindSpellTargetAction
        : AbstractTaskAction<FindSpellTargetComponent, FindSpellTargetTag, FindSpellTargetSystem>, IAction
    {
        protected override FindSpellTargetComponent CreateBufferElement(ushort runtimeIndex)
            => new FindSpellTargetComponent { Index = runtimeIndex };
    }

    public struct FindSpellTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct FindSpellTargetTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class FindSpellTargetSystem : TaskProcessorSystem<FindSpellTargetComponent, FindSpellTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;
        ComponentLookup<HealthMirror> _hpRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO  = GetComponentLookup<LocalTransform>(true);
            _factRO = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            _hpRO   = GetComponentLookup<HealthMirror>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            _hpRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;

            // Must have a baked spell config to evaluate anything.
            if (!em.HasComponent<SpellConfig>(e))
                return TaskStatus.Failure;

            var cfg = em.GetComponentData<SpellConfig>(e);

            switch (cfg.TargetType)
            {
                // KISS: MultiTarget uses the same viability test as SingleTarget here.
                case OneBitRob.SpellTargetType.SingleTarget:
                case OneBitRob.SpellTargetType.MultiTarget:
                {
                    Entity tgt;
                    switch (cfg.Strategy)
                    {
                        case OneBitRob.SpellTargetingStrategyType.LowestHealthAlly:
                            tgt = new LowestHealthAllyTargeting()
                                .GetTarget(e, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                            break;

                        case OneBitRob.SpellTargetingStrategyType.ClosestEnemy:
                        default:
                            tgt = new ClosestEnemySpellTargeting()
                                .GetTarget(e, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                            break;
                    }
                    return tgt != Entity.Null ? TaskStatus.Success : TaskStatus.Failure;
                }

                case OneBitRob.SpellTargetType.AreaOfEffect:
                {
                    bool ok;
                    switch (cfg.Strategy)
                    {
                        case OneBitRob.SpellTargetingStrategyType.DensestCluster:
                        {
                            float3 dummyPoint; // typed temp avoids 'out _' issues
                            ok = new DensestEnemyClusterTargeting()
                                .TryGetAOETargetPoint(e, in cfg, ref _posRO, ref _factRO, out dummyPoint);
                            break;
                        }

                        // Fallback AoE: center on closest enemy if no cluster strategy requested.
                        case OneBitRob.SpellTargetingStrategyType.ClosestEnemy:
                        default:
                        {
                            var closest = new ClosestEnemySpellTargeting()
                                .GetTarget(e, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                            ok = (closest != Entity.Null);
                            break;
                        }
                    }
                    return ok ? TaskStatus.Success : TaskStatus.Failure;
                }
            }

            return TaskStatus.Failure;
        }
    }
}
