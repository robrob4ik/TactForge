// FILE: OneBitRob/AI/HasCastDecisionConditional.cs

using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Success only if SpellDecisionSystem produced a CastRequest this frame")]
    public class HasCastDecisionConditional
        : AbstractTaskAction<HasCastDecisionComponent, HasCastDecisionTag, HasCastDecisionSystem>, IConditional
    {
        protected override HasCastDecisionComponent CreateBufferElement(ushort runtimeIndex)
            => new HasCastDecisionComponent { Index = runtimeIndex };
    }

    public struct HasCastDecisionComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct HasCastDecisionTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellPlanSystem))]   // decision exists…
    [UpdateBefore(typeof(SpellWindupAndFireSystem))] // …before execution consumes it
    public partial class HasCastDecisionSystem
        : TaskProcessorSystem<HasCastDecisionComponent, HasCastDecisionTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<CastRequest>(e)) return TaskStatus.Failure;
            var cr = em.GetComponentData<CastRequest>(e);
            return cr.HasValue != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}