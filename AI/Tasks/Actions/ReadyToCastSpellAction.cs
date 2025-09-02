using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("ReadyToCastSpellAction")]
    public class ReadyToCastSpellAction
        : AbstractTaskAction<ReadyToCastSpellComponent, ReadyToCastSpellTag, ReadyToCastSpellSystem>, IAction
    {
        protected override ReadyToCastSpellComponent CreateBufferElement(ushort runtimeIndex)
            => new ReadyToCastSpellComponent { Index = runtimeIndex };
    }

    public struct ReadyToCastSpellComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct ReadyToCastSpellTag       : IComponentData, IEnableableComponent { }

    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateBefore(typeof(SpellPlanSystem))] // <-- same frame, set decision request first
    public partial class ReadyToCastSpellSystem
        : TaskProcessorSystem<ReadyToCastSpellComponent, ReadyToCastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellDecisionRequest>(e))
                return TaskStatus.Failure;

            var req = em.GetComponentData<SpellDecisionRequest>(e);
            req.HasValue = 1; // ask ECS to produce CastRequest now
            em.SetComponentData(e, req);

            return TaskStatus.Success;
        }
    }
}