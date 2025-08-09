using OneBitRob.Constants;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Finds a valid target via SpatialHash and writes Target component")]
    public class FindTargetAction : AbstractTaskAction<FindTargetComponent, FindTargetTag, FindTargetSystem>, IAction
    {
        protected override FindTargetComponent CreateBufferElement(ushort runtimeIndex)
            => new FindTargetComponent { Index = runtimeIndex };
    }

    public struct FindTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct FindTargetTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class FindTargetSystem : TaskProcessorSystem<FindTargetComponent, FindTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO  = GetComponentLookup<LocalTransform>(true);
            _factRO = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {   
            FixedList128Bytes<byte> wanted = default;
            wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

            float range = brain.UnitDefinition.targetDetectionRange;
            if (range <= 0f) range = 100f;

            var selfPos = SystemAPI.GetComponent<LocalTransform>(e).Position;
            var closest = SpatialHashSearch.GetClosest(selfPos, range, wanted, ref _posRO, ref _factRO);
            if (closest == Entity.Null)
                return TaskStatus.Failure;

            // Hysteresis: only replace an existing valid target if the new one is
            // at least autoTargetMinSwitchDistance units closer.
            if (EntityManager.HasComponent<Target>(e))
            {
                var current = EntityManager.GetComponentData<Target>(e).Value;
                if (current != Entity.Null && _posRO.HasComponent(current))
                {
                    float minSwitch = math.max(0f, brain.UnitDefinition.autoTargetMinSwitchDistance);
                    if (minSwitch > 0f)
                    {
                        float distCurr = math.distance(_posRO[current].Position, selfPos);
                        float distCand = math.distance(_posRO[closest].Position, selfPos);

                        // If the candidate is not at least minSwitch units closer, keep current.
                        if ((distCurr - distCand) < minSwitch)
                            return TaskStatus.Success;
                    }
                }
            }

            EntityManager.SetComponentData(e, new Target { Value = closest });
            return TaskStatus.Success;
        }
    }
}
