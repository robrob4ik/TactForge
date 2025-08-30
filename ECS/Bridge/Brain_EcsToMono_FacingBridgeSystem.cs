// ECS/HybridSync/EcsToMono/Brain_EcsToMono_FacingBridgeSystem.cs

using OneBitRob.AI;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// For each DesiredFacing with HasValue=1, force Mono facing.
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_FacingBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (df, e) in SystemAPI.Query<RefRW<DesiredFacing>>().WithEntityAccess())
            {
                if (df.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    var facePos = (Vector3)df.ValueRO.TargetPosition;
                    brain.SetForcedFacing(facePos);
#if UNITY_EDITOR
                    Debug.DrawLine(brain.transform.position, facePos, Color.yellow, 0f, false);
#endif
                }
                df.ValueRW = default; // consume
            }
        }
    }
}