using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Rotating to spell target (writes DesiredFacing)")]
    public class RotateToSpellTargetAction
        : AbstractTaskAction<RotateToSpellTargetComponent, RotateToSpellTargetTag, RotateToSpellTargetSystem>, IAction
    {
        protected override RotateToSpellTargetComponent CreateBufferElement(ushort runtimeIndex)
            => new RotateToSpellTargetComponent { Index = runtimeIndex };
    }

    public struct RotateToSpellTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct RotateToSpellTargetTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class RotateToSpellTargetSystem
        : TaskProcessorSystem<RotateToSpellTargetComponent, RotateToSpellTargetTag>
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
            // Prefer explicit spell point if we have it
            Vector3? desiredPoint = brain.CurrentSpellTargetPosition;

            // Else rotate toward the spell target GameObject if available
            if (!desiredPoint.HasValue && brain.CurrentSpellTarget != null)
                desiredPoint = brain.CurrentSpellTarget.transform.position;

            // Else fall back to normal target entity (via Target component)
            if (!desiredPoint.HasValue && EntityManager.HasComponent<Target>(e))
            {
                var tgtEnt = EntityManager.GetComponentData<Target>(e).Value;
                if (tgtEnt != Entity.Null && _posRO.HasComponent(tgtEnt))
                    desiredPoint = _posRO[tgtEnt].Position;
            }

            if (!desiredPoint.HasValue) return TaskStatus.Failure;

            if (!EntityManager.HasComponent<DesiredFacing>(e))
                EntityManager.AddComponentData(e, new DesiredFacing { TargetPosition = desiredPoint.Value, HasValue = 1 });
            else
            {
                var df = EntityManager.GetComponentData<DesiredFacing>(e);
                df.TargetPosition = desiredPoint.Value;
                df.HasValue = 1;
                EntityManager.SetComponentData(e, df);
            }

#if UNITY_EDITOR
            Debug.DrawLine(brain.transform.position, desiredPoint.Value, Color.yellow, 0f, false);
#endif
            return TaskStatus.Success;
        }
    }
}
