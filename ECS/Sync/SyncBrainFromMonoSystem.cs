using OneBitRob.AI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.ECS.Sync
{
    // Mirror GameObject transform -> brain entity LocalTransform.
    // Run before spatial hash so all readers see fresh positions.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SpatialHashBuildSystem))]
    public partial class SyncBrainFromMonoSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (lt, e) in SystemAPI
                         .Query<RefRW<LocalTransform>>()
                         .WithAll<AgentTag>()            // brain entities created in SpawnerSystem
                         .WithEntityAccess())
            {
                var brain = UnitBrainRegistry.Get(e);
                if (!brain) continue;               // not yet registered

                var t = brain.transform;
                var v = lt.ValueRO;
                v.Position = t.position;
                v.Rotation = (quaternion)t.rotation; // optional for top‑down
                lt.ValueRW = v;

#if UNITY_EDITOR
                Debug.DrawLine(t.position, t.position + Vector3.up * 0.6f, Color.magenta, 0f, false);
#endif
            }
        }
    }
}