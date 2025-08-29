using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("CanCastSpell (Conditional)")]
    public class CanCastSpellConditional
        : AbstractTaskAction<CanCastSpellComponent, CanCastSpellTag, CanCastSpellSystem>, IConditional
    {
        protected override CanCastSpellComponent CreateBufferElement(ushort runtimeIndex)
            => new CanCastSpellComponent { Index = runtimeIndex };
    }

    public struct CanCastSpellComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct CanCastSpellTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class CanCastSpellSystem : TaskProcessorSystem<CanCastSpellComponent, CanCastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellState>(e))
                return TaskStatus.Failure;

            // Symmetry with attack gating: if we're mid-attack windup or locked for attack, don't allow casting
            if (em.HasComponent<AttackWindup>(e) && em.GetComponentData<AttackWindup>(e).Active != 0)
                return TaskStatus.Failure;

            if (em.HasComponent<MovementLock>(e))
            {
                var f = em.GetComponentData<MovementLock>(e).Flags;
                if ((f & MovementLockFlags.Attacking) != 0) return TaskStatus.Failure;
            }

            var ss = em.GetComponentData<SpellState>(e);
            return ss.Ready != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}