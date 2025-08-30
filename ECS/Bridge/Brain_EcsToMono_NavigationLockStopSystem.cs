// FILE: Assets/PROJECT/Scripts/Runtime/ECS/HybridSync/Brain_EcsToMono_NavigationLockStopSystem.cs
// Summary: hard-stop agent while locked (Casting/Attacking), then park destination at self.

using OneBitRob.AI;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    [UpdateAfter(typeof(Brain_EcsToMono_NavigationBridgeSystem))]
    public partial struct Brain_EcsToMono_NavigationLockStopSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            foreach (var (ml, e) in SystemAPI.Query<RefRO<MovementLock>>().WithEntityAccess())
            {
                var flags = ml.ValueRO.Flags;
                if ((flags & (MovementLockFlags.Casting | MovementLockFlags.Attacking)) == 0)
                    continue;

                var brain = UnitBrainRegistry.Get(e);
                if (!brain) continue;

                // Hard stop the underlying nav body and park destination.
                brain.StopAgentMotion();
                brain.MoveToPosition(brain.transform.position);
            }
        }
    }
}