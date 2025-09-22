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
    public struct AttackTargetTag       : IComponentData, IEnableableComponent { }

    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class AttackTargetSystem : TaskProcessorSystem<AttackTargetComponent, AttackTargetTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;

            // Guards
            if (TaskGuards.IsBlockedByCastOrAttack(em, e)) return TaskStatus.Failure;
            if (!em.HasComponent<Target>(e))               return TaskStatus.Failure;

            var target = em.GetComponentData<Target>(e).Value;
            if (target == Entity.Null) return TaskStatus.Failure;

            // Upsert & enable request
            if (!em.HasComponent<AttackRequest>(e)) em.AddComponent<AttackRequest>(e);
            var req = em.GetComponentData<AttackRequest>(e);
            req.Target = target;
            em.SetComponentData(e, req);
            em.SetComponentEnabled<AttackRequest>(e, true);

            return TaskStatus.Success;
        }
    }
}