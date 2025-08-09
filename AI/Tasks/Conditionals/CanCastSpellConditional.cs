using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Uses DOTS to determine if unit can cast spell")]
    public class CanCastSpellConditional : AbstractTaskAction<CanCastSpellComponent, CanCastSpellTag, CanCastSpellSystem>, IConditional
    {
        protected override CanCastSpellComponent CreateBufferElement(ushort runtimeIndex) { return new CanCastSpellComponent { Index = runtimeIndex }; }
    }

    public struct CanCastSpellComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct CanCastSpellTag : IComponentData, IEnableableComponent
    {
    }
    
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class CanCastSpellSystem
        : TaskProcessorSystem<CanCastSpellComponent, CanCastSpellTag>
    {
        protected override TaskStatus Execute(Entity _, UnitBrain brain) => brain.CanCastSpell() ? TaskStatus.Success : TaskStatus.Failure;
    }
}