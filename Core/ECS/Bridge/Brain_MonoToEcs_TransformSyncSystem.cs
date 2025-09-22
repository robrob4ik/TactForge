using OneBitRob.AI;
using OneBitRob.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(MonoToEcsSyncGroup))]
    public partial struct Brain_MonoToEcs_TransformSyncSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (transformRW, entity) in SystemAPI
                         .Query<RefRW<LocalTransform>>()
                         .WithAll<AgentTag>()
                         .WithEntityAccess())
            {
                var brain = UnitBrainRegistry.Get(entity);
                if (!brain) continue;

                var t = brain.transform;
                var value = transformRW.ValueRO;
                value.Position = t.position;
                value.Rotation = (Unity.Mathematics.quaternion)t.rotation;
                transformRW.ValueRW = value;

#if UNITY_EDITOR
                Debug.DrawLine(t.position, t.position + Vector3.up * 0.6f, DebugPalette.TransformSync, 0.35f, false);
#endif
            }
        }
    }
}