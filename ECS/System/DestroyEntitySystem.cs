using System.Collections.Generic;
using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    public struct DestroyEntityTag : IComponentData, IEnableableComponent { }

    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
    public partial class DestroyEntitySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb       = new EntityCommandBuffer(Allocator.Temp);
            var toDestroy = new List<GameObject>(); // Collect GOs here

            foreach (var (destroyTag, entity) in SystemAPI.Query<RefRO<DestroyEntityTag>>().WithEntityAccess())
            {
                var brain = UnitBrainRegistry.Get(entity);
                if (brain && brain.gameObject)
                {
                    toDestroy.Add(brain.gameObject);
                    if (brain.TryGetComponent<GPUIPrefab>(out var gpuiPrefab))
                        GPUIPrefabAPI.RemovePrefabInstance(gpuiPrefab);
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            foreach (var go in toDestroy)
                GameObject.Destroy(go);
        }
    }
}