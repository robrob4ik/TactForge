using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Returns Success while the current target is alive (pure ECS)")]
    public class IsTargetAliveConditional
        : AbstractTaskAction<IsTargetAliveComponent, IsTargetAliveTag, IsTargetAliveSystem>, IConditional
    {
        protected override IsTargetAliveComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsTargetAliveComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsTargetAliveTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class IsTargetAliveSystem
        : TaskProcessorSystem<IsTargetAliveComponent, IsTargetAliveTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<Target>(e)) return TaskStatus.Failure;
            var target = em.GetComponentData<Target>(e).Value;
            if (target == Entity.Null || !em.HasComponent<Alive>(target)) return TaskStatus.Failure;
            return em.GetComponentData<Alive>(target).Value != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}