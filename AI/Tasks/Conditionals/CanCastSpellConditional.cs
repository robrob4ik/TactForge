using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Uses DOTS flag to determine if unit can cast spell (pure ECS)")]
    public class CanCastSpellConditional : AbstractTaskAction<CanCastSpellComponent, CanCastSpellTag, CanCastSpellSystem>, IConditional
    {
        protected override CanCastSpellComponent CreateBufferElement(ushort runtimeIndex) { return new CanCastSpellComponent { Index = runtimeIndex }; }
    }

    public struct CanCastSpellComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct CanCastSpellTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class CanCastSpellSystem
        : TaskProcessorSystem<CanCastSpellComponent, CanCastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellState>(e)) return TaskStatus.Failure;
            return em.GetComponentData<SpellState>(e).CanCast != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}