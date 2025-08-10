using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Entities;
using UnityEngine;

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
            if (!EntityManager.HasComponent<Target>(e)) return TaskStatus.Failure;

            var target = EntityManager.GetComponentData<Target>(e).Value;
            if (target == Entity.Null) return TaskStatus.Failure;

            if (!EntityManager.HasComponent<AttackRequest>(e))
                EntityManager.AddComponentData(e, new AttackRequest { Target = target, HasValue = 1 });
            else
            {
                var req = EntityManager.GetComponentData<AttackRequest>(e);
                req.Target = target;
                req.HasValue = 1;
                EntityManager.SetComponentData(e, req);
            }

            return TaskStatus.Success;
        }
    }
}