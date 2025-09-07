using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Transforms;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Rotating to target (writes DesiredFacing)")]
    public class RotateToTargetAction : AbstractTaskAction<RotateToTargetComponent, RotateToTargetTag, RotateToTargetSystem>, IAction
    {
        protected override RotateToTargetComponent CreateBufferElement(ushort runtimeIndex) => new RotateToTargetComponent { Index = runtimeIndex };
    }

    public struct RotateToTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct RotateToTargetTag : IComponentData, IEnableableComponent { }
    
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class RotateToTargetSystem : TaskProcessorSystem<RotateToTargetComponent, RotateToTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO = GetComponentLookup<LocalTransform>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            if (!EntityManager.HasComponent<Target>(e)) return TaskStatus.Failure;

            var tgt = EntityManager.GetComponentData<Target>(e).Value;
            if (tgt == Entity.Null || !_posRO.HasComponent(tgt)) return TaskStatus.Failure;

            var pos = _posRO[tgt].Position;

            if (!EntityManager.HasComponent<DesiredFacing>(e))
                EntityManager.AddComponentData(e, new DesiredFacing { TargetPosition = pos, HasValue = 1 });
            else
            {
                var df = EntityManager.GetComponentData<DesiredFacing>(e);
                df.TargetPosition = pos;
                df.HasValue = 1;
                EntityManager.SetComponentData(e, df);
            }

            return TaskStatus.Success;
        }
    }
}
