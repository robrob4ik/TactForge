using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Unit’s combat style is Melee")]
    public class IsMeleeConditional
        : AbstractTaskAction<IsMeleeComponent, IsMeleeTag, IsMeleeSystem>, IConditional
    {
        protected override IsMeleeComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsMeleeComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsMeleeTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    public partial class IsMeleeSystem
        : TaskProcessorSystem<IsMeleeComponent, IsMeleeTag>
    {
        protected override TaskStatus Execute(Entity _, UnitBrain brain)
            => brain.UnitDefinition.combatStrategy == CombatStrategyType.Melee
                ? TaskStatus.Success
                : TaskStatus.Failure;
    }
}