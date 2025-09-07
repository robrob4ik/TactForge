using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Success when target is within attack range (pure ECS flag)")]
    public class IsTargetInAttackRangeConditional : AbstractTaskAction<IsTargetInAttackRangeComponent, IsTargetInAttackRangeTag, IsTargetInAttackRangeSystem>, IConditional
    {
        protected override IsTargetInAttackRangeComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsTargetInAttackRangeComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsTargetInAttackRangeTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class IsTargetInAttackRangeSystem : TaskProcessorSystem<IsTargetInAttackRangeComponent, IsTargetInAttackRangeTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<InAttackRange>(e)) return TaskStatus.Failure;
            var flag = em.GetComponentData<InAttackRange>(e);
            return flag.Value != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}