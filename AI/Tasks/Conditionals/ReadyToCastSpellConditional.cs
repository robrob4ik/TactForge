using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Uses DOTS to determine if unit ready to cast spell")]
    public class ReadyToCastSpellConditional : AbstractTaskAction<ReadyToCastSpellComponent, ReadyToCastSpellTag, ReadyToCastSpellSystem>, IConditional
    {
        protected override ReadyToCastSpellComponent CreateBufferElement(ushort runtimeIndex) { return new ReadyToCastSpellComponent { Index = runtimeIndex }; }
    }


    public struct ReadyToCastSpellComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct ReadyToCastSpellTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class ReadyToCastSpellSystem
        : TaskProcessorSystem<ReadyToCastSpellComponent, ReadyToCastSpellTag>
    {
        protected override TaskStatus Execute(Entity _, UnitBrain brain) => brain.ReadyToCastSpell() ? TaskStatus.Success : TaskStatus.Failure;
    }
}