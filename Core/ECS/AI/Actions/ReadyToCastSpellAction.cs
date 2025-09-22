using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    public class ReadyToCastSpellAction : AbstractTaskAction<ReadyToCastSpellComponent, ReadyToCastSpellTag, ReadyToCastSpellSystem>, IAction
    {
        protected override ReadyToCastSpellComponent CreateBufferElement(ushort runtimeIndex) => new ReadyToCastSpellComponent { Index = runtimeIndex };
    }

    public struct ReadyToCastSpellComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct ReadyToCastSpellTag       : IComponentData, IEnableableComponent { }

    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class ReadyToCastSpellSystem : TaskProcessorSystem<ReadyToCastSpellComponent, ReadyToCastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellDecisionRequest>(e)) return TaskStatus.Failure;

            // Enable request (consumed by SpellPlanSystem this frame)
            em.SetComponentEnabled<SpellDecisionRequest>(e, true);
            return TaskStatus.Success;
        }
    }
}