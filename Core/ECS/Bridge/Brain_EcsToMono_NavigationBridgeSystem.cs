using OneBitRob.AI;
using OneBitRob.Core;
using OneBitRob.Debugging;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_NavigationBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            foreach (var (desiredDestination, entity) in SystemAPI.Query<RefRW<DesiredDestination>>().WithEntityAccess())
            {
                if (desiredDestination.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(entity);
                if (!brain) { desiredDestination.ValueRW = default; continue; }

                // NEW: treat Attacking as a lock just like Casting
                bool locked = false;
                if (em.HasComponent<MovementLock>(entity))
                {
                    var flags = em.GetComponentData<MovementLock>(entity).Flags;
                    locked = (flags & (MovementLockFlags.Casting | MovementLockFlags.Attacking)) != 0;
                }

                Vector3 wanted = locked ? brain.transform.position : desiredDestination.ValueRO.Position;

                // avoid noise
                if ((wanted - brain.CurrentTargetPosition).sqrMagnitude > 0.0004f)
                    brain.MoveToPosition(wanted);

#if UNITY_EDITOR
                DebugDraw.Line(brain.transform.position, wanted, locked ? DebugPalette.NavLocked : DebugPalette.MoveIntent);
#endif
                desiredDestination.ValueRW = default; // consume
            }
        }
    }
}