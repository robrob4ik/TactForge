using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_FacingBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (desiredFacing, entity) in SystemAPI.Query<RefRW<DesiredFacing>>().WithEntityAccess())
            {
                if (desiredFacing.ValueRO.HasValue == 0) continue;

                var brain = OneBitRob.AI.UnitBrainRegistry.Get(entity);
                if (brain)
                {
                    var facePos = (Vector3)desiredFacing.ValueRO.TargetPosition;
                    brain.SetForcedFacing(facePos);
#if UNITY_EDITOR
                    Debug.DrawLine(brain.transform.position, facePos, Color.yellow, 0.6f, false);
#endif
                }
                desiredFacing.ValueRW = default; // consume
            }
        }
    }
}