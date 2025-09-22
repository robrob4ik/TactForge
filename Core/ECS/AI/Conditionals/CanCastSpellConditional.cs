using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Entities;

namespace OneBitRob.AI
{
    public class CanCastSpellConditional : AbstractTaskAction<CanCastSpellComponent, CanCastSpellTag, CanCastSpellSystem>, IConditional
    {
        protected override CanCastSpellComponent CreateBufferElement(ushort runtimeIndex) => new CanCastSpellComponent { Index = runtimeIndex };
    }

    public struct CanCastSpellComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct CanCastSpellTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class CanCastSpellSystem : TaskProcessorSystem<CanCastSpellComponent, CanCastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellState>(e))
                return TaskStatus.Failure;
            
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