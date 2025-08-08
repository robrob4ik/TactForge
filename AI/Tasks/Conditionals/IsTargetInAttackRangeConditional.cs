using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Success when target is within attack range")]
    public class IsTargetInAttackRangeConditional
        : AbstractTaskAction<IsTargetInAttackRangeComponent, IsTargetInAttackRangeTag, IsTargetInAttackRangeSystem>, IConditional
    {
        protected override IsTargetInAttackRangeComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsTargetInAttackRangeComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsTargetInAttackRangeTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    public partial class IsTargetInAttackRangeSystem
        : TaskProcessorSystem<IsTargetInAttackRangeComponent, IsTargetInAttackRangeTag>
    {
        protected override TaskStatus Execute(Entity _, UnitBrain brain)
        {
            if (brain.CurrentTarget == null) return TaskStatus.Failure;
            return brain.IsTargetInAttackRange(brain.CurrentTarget)
                ? TaskStatus.Success
                : TaskStatus.Failure;
        }
    }
}