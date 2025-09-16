// Assets/PROJECT/Scripts/ECS/Brain_EcsEntityCleanupSystem.cs
using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.VFX;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
    public partial class Brain_EcsEntityCleanupSystem : SystemBase
    {
        // Simple, global death hold to let anim + nested FX finish
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
                    // If registered with GPUI, unregister
                    if (brain.TryGetComponent<GPUIPrefab>(out var gpuiPrefab))
                        GPUIPrefabAPI.RemovePrefabInstance(gpuiPrefab);

                    // Destroy the ECS entity now
                    ecb.DestroyEntity(entity);

                    // Let the GO stick around a bit for death anim + nested FX
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
