using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Unit’s combat style is Ranged (pure ECS)")]
    public class IsRangedConditional : AbstractTaskAction<IsRangedComponent, IsRangedTag, IsRangedSystem>, IConditional
    {
        protected override IsRangedComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsRangedComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsRangedTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class IsRangedSystem : TaskProcessorSystem<IsRangedComponent, IsRangedTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<CombatStyle>(e)) return TaskStatus.Failure;
            return em.GetComponentData<CombatStyle>(e).Value == 2 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}