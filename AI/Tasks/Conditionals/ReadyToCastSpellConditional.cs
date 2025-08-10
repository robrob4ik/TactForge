using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Uses DOTS flag to determine if unit ready to cast spell (pure ECS)")]
    public class ReadyToCastSpellConditional : AbstractTaskAction<ReadyToCastSpellComponent, ReadyToCastSpellTag, ReadyToCastSpellSystem>, IConditional
    {
        protected override ReadyToCastSpellComponent CreateBufferElement(ushort runtimeIndex) { return new ReadyToCastSpellComponent { Index = runtimeIndex }; }
    }

    public struct ReadyToCastSpellComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct ReadyToCastSpellTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class ReadyToCastSpellSystem
        : TaskProcessorSystem<ReadyToCastSpellComponent, ReadyToCastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellState>(e)) return TaskStatus.Failure;
            return em.GetComponentData<SpellState>(e).Ready != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}