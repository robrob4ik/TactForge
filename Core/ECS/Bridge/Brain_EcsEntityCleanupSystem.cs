// File: Assets/PROJECT/Scripts/ECS/Brain/Brain_EcsEntityCleanupSystem.cs

using System.Reflection;
using GPUInstancerPro.PrefabModule;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using OneBitRob.AI;
using OneBitRob.VFX;
using OneBitRob.FX;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
    public partial class Brain_EcsEntityCleanupSystem : SystemBase
    {
        // Let death anim + nested FX finish
        private const float DeathDespawnDelaySeconds = 1.0f;

        protected override void OnUpdate()
        {
            var em  = EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (destroyTag, entity) in SystemAPI.Query<RefRO<DestroyEntityTag>>().WithEntityAccess())
            {
                // Release any target-attached VFX held by this entity (caster)
                if (em.HasComponent<ActiveTargetVfx>(entity))
                {
                    var bind = em.GetComponentData<ActiveTargetVfx>(entity);
                    VfxPoolManager.EndPersistent(bind.Key);
                    ecb.RemoveComponent<ActiveTargetVfx>(entity);
                }

                var brain = UnitBrainRegistry.Get(entity);
                if (brain && brain.gameObject)
                {
                    // IMPORTANT: rescue pooled FX parented under the unit so they don't get destroyed with it
                    FeedbackService.RescuePooledChildren(brain.transform);

                    // If registered with GPUI, unregister
                    if (brain.TryGetComponent<GPUIPrefab>(out var gpuiPrefab))
                        GPUIPrefabAPI.RemovePrefabInstance(gpuiPrefab);

                    // Destroy the ECS entity now
                    ecb.DestroyEntity(entity);

                    // Destroy the GO after a small delay
                    GameObject.Destroy(brain.gameObject, DeathDespawnDelaySeconds);
                }
                else
                {
                    // No brain/GO? just destroy the entity
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}
