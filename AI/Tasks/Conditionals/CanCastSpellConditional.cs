using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;
using UnityEditor.Rendering;
using UnityEngine;

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
            if (!EntityManager.HasComponent<SpellConfig>(e)) return TaskStatus.Failure;
            if (!EntityManager.HasComponent<SpellState>(e)) return TaskStatus.Failure;

            var ss = EntityManager.GetComponentData<SpellState>(e);
            return ss.Ready != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
