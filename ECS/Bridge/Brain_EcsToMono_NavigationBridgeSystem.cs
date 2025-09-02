using OneBitRob.AI;
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

                bool casting = em.HasComponent<MovementLock>(entity) && (em.GetComponentData<MovementLock>(entity).Flags & MovementLockFlags.Casting) != 0;

                Vector3 wanted = casting ? brain.transform.position : desiredDestination.ValueRO.Position;

                // avoid noise
                if ((wanted - brain.CurrentTargetPosition).sqrMagnitude > 0.0004f)
                    brain.MoveToPosition(wanted);


                DebugDraw.Line(brain.transform.position, wanted, casting ? new Color(1f, 0.6f, 0.1f, 0.9f) : Color.cyan);

                desiredDestination.ValueRW = default; // consume
            }
        }
    }
}