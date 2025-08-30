// ECS/HybridSync/Cleanup/Brain_EcsEntityCleanupSystem.cs
using System.Collections.Generic;
using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.VFX;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// Destroys ECS entity & matching Mono GameObject when DestroyEntityTag is present.
    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
    public partial class Brain_EcsEntityCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb       = new EntityCommandBuffer(Allocator.Temp);
            var toDestroy = new List<GameObject>();

            foreach (var (destroyTag, entity) in SystemAPI.Query<RefRO<DestroyEntityTag>>().WithEntityAccess())
            {
                // Release any target-attached VFX held by this entity (caster)
                if (EntityManager.HasComponent<ActiveTargetVfx>(entity))
                {
                    var bind = EntityManager.GetComponentData<ActiveTargetVfx>(entity);
                    VfxPoolManager.EndPersistent(bind.Key);
                    ecb.RemoveComponent<ActiveTargetVfx>(entity);
                }

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