using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Unit’s combat style is Ranged")]
    public class IsRangedConditional
        : AbstractTaskAction<IsRangedComponent, IsRangedTag, IsRangedSystem>, IConditional
    {
        protected override IsRangedComponent CreateBufferElement(ushort runtimeIndex) => new() { Index = runtimeIndex };
    }

    public struct IsRangedComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsRangedTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class IsRangedSystem
        : TaskProcessorSystem<IsRangedComponent, IsRangedTag>
    {
        protected override TaskStatus Execute(Entity _, UnitBrain brain)
            => brain.UnitDefinition.combatStrategy == CombatStrategyType.Ranged
                ? TaskStatus.Success
                : TaskStatus.Failure;
    }
}