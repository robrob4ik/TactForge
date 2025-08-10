using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Unit’s combat style is Melee (pure ECS)")]
    public class IsMeleeConditional
        : AbstractTaskAction<IsMeleeComponent, IsMeleeTag, IsMeleeSystem>, IConditional
    {
        protected override IsMeleeComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsMeleeComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsMeleeTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class IsMeleeSystem
        : TaskProcessorSystem<IsMeleeComponent, IsMeleeTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<CombatStyle>(e)) return TaskStatus.Failure;
            return em.GetComponentData<CombatStyle>(e).Value == 1 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}