using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Rotating to target")]
    public class RotateToTargetAction : AbstractTaskAction<RotateToTargetComponent, RotateToTargetTag, RotateToTargetSystem>, IAction
    {
        protected override RotateToTargetComponent CreateBufferElement(ushort runtimeIndex) { return new RotateToTargetComponent { Index = runtimeIndex }; }
    }

    public struct RotateToTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct RotateToTargetTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    public partial class RotateToTargetSystem
        : TaskProcessorSystem<RotateToTargetComponent, RotateToTargetTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            if (brain.CurrentTarget == null) return TaskStatus.Failure;

            brain.RotateToTarget();
            return TaskStatus.Success;
        }
    }
        
    
}