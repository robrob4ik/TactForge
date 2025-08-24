// ECS/HybridSync/EcsToMono/Brain_EcsToMono_NavigationBridgeSystem.cs
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// For each DesiredDestination with HasValue=1, drive the Mono navigation.
    /// Honors MovementLock.Casting (park in place while casting).
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_NavigationBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            foreach (var (dd, e) in SystemAPI.Query<RefRW<DesiredDestination>>().WithEntityAccess())
            {
                if (dd.ValueRO.HasValue == 0) continue;

                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (!brain) { dd.ValueRW = default; continue; }

                bool casting = em.HasComponent<MovementLock>(e) &&
                               (em.GetComponentData<MovementLock>(e).Flags & MovementLockFlags.Casting) != 0;

                Vector3 wanted = casting ? brain.transform.position : (Vector3)dd.ValueRO.Position;

                // avoid noise
                if ((wanted - brain.CurrentTargetPosition).sqrMagnitude > 0.0004f)
                    brain.MoveToPosition(wanted);

#if UNITY_EDITOR
                Debug.DrawLine(brain.transform.position, wanted,
                    casting ? new Color(1f, 0.6f, 0.1f, 0.9f) : Color.cyan, 0f, false);
#endif
                dd.ValueRW = default; // consume
            }
        }
    }
}