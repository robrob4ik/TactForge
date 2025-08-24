// ECS/HybridSync/MonoToEcs/Brain_MonoToEcs_TransformSyncSystem.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// Mirrors UnitBrain's Transform -> LocalTransform (position/rotation).
    [UpdateInGroup(typeof(MonoToEcsSyncGroup))]
    [UpdateBefore(typeof(OneBitRob.ECS.SpatialHashBuildSystem))]
    public partial struct Brain_MonoToEcs_TransformSyncSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (lt, e) in SystemAPI
                         .Query<RefRW<LocalTransform>>()
                         .WithAll<AgentTag>()
                         .WithEntityAccess())
            {
                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (!brain) continue;

                var t = brain.transform;
                var v = lt.ValueRO;
                v.Position = t.position;
                v.Rotation = (quaternion)t.rotation; // keep rotation for facing logic
                lt.ValueRW = v;

#if UNITY_EDITOR
                Debug.DrawLine(t.position, t.position + Vector3.up * 0.6f, Color.magenta, 0f, false);
#endif
            }
        }
    }
}