using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Entities;

namespace OneBitRob.AI
{
    public class AttackTargetAction : AbstractTaskAction<AttackTargetComponent, AttackTargetTag, AttackTargetSystem>, IAction
    {
        protected override AttackTargetComponent CreateBufferElement(ushort runtimeIndex) => new AttackTargetComponent { Index = runtimeIndex };
    }

    public struct AttackTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct AttackTargetTag : IComponentData, IEnableableComponent { }

    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class AttackTargetSystem : TaskProcessorSystem<AttackTargetComponent, AttackTargetTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var em = EntityManager;

            // Block while casting, ranged windup, or any explicit movement lock
            if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                return TaskStatus.Failure;

            if (em.HasComponent<AttackWindup>(e) && em.GetComponentData<AttackWindup>(e).Active != 0)
                return TaskStatus.Failure;

            if (em.HasComponent<MovementLock>(e))
            {
                var f = em.GetComponentData<MovementLock>(e).Flags;
                if ((f & (MovementLockFlags.Casting | MovementLockFlags.Attacking)) != 0)
                    return TaskStatus.Failure;
            }

            if (!em.HasComponent<Target>(e)) return TaskStatus.Failure;

            var target = em.GetComponentData<Target>(e).Value;
            if (target == Entity.Null) return TaskStatus.Failure;

            if (!em.HasComponent<AttackRequest>(e))
                em.AddComponentData(e, new AttackRequest { Target = target, HasValue = 1 });
            else
            {
                var req = em.GetComponentData<AttackRequest>(e);
                req.Target = target;
                req.HasValue = 1;
                em.SetComponentData(e, req);
            }

            return TaskStatus.Success;
        }
    }
}
