using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    public class IsAllyConditional : AbstractTaskAction<IsAllyComponent, IsAllyTag, IsAllySystem>, IConditional
    {
        protected override IsAllyComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsAllyComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsAllyTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class IsAllySystem : TaskProcessorSystem<IsAllyComponent, IsAllyTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (em.HasComponent<AllyTag>(e)) return TaskStatus.Success;;
            return TaskStatus.Failure;
        }
    }
}