using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Returns Success while the current target is alive")]
    public class IsTargetAliveConditional
        : AbstractTaskAction<IsTargetAliveComponent, IsTargetAliveTag, IsTargetAliveSystem>, IConditional
    {
        protected override IsTargetAliveComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsTargetAliveComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsTargetAliveTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    public partial class IsTargetAliveSystem
        : TaskProcessorSystem<IsTargetAliveComponent, IsTargetAliveTag>
    {
        protected override TaskStatus Execute(Entity _, UnitBrain brain)
        {
            if (brain.CurrentTarget == null) return TaskStatus.Failure;
            return brain.IsTargetAlive() ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}