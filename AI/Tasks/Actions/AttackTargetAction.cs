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

    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class AttackTargetSystem : TaskProcessorSystem<AttackTargetComponent, AttackTargetTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var em = EntityManager;

            // 1) Guards
            if (TaskGuards.IsBlockedByCastOrAttack(em, e))          
                return TaskStatus.Failure;
            if (!TryGetValidTarget(em, e, out var target))          
                return TaskStatus.Failure;

            // 2) Enqueue the attack request for this frame
            UpsertAttackRequest(em, e, target);

            return TaskStatus.Success;
        }

        private static bool TryGetValidTarget(EntityManager em, Entity e, out Entity target)
        {
            target = Entity.Null;
            if (!em.HasComponent<Target>(e)) return false;

            target = em.GetComponentData<Target>(e).Value;
            return target != Entity.Null;
        }

        private static void UpsertAttackRequest(EntityManager em, Entity e, Entity target)
        {
            var req = new AttackRequest { Target = target, HasValue = 1 };
            if (em.HasComponent<AttackRequest>(e)) em.SetComponentData(e, req);
            else                                    em.AddComponentData(e, req);
        }
    }
}
