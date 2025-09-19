// File: OneBitRob/ECS/Brain_EcsToMono_WeaponProjectileBridgeSystem.cs

using OneBitRob.Debugging;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_WeaponProjectileBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (spawnRW, entity) in SystemAPI.Query<RefRW<EcsProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<EcsProjectileSpawnRequest>(entity)) continue;
                var spawn = spawnRW.ValueRO;

                var brain = OneBitRob.AI.UnitBrainRegistry.Get(entity);
                if (brain && brain.UnitCombatController != null)
                {
                    Vector3 origin = (Vector3)spawn.Origin;
                    Vector3 dir    = ((Vector3)spawn.Direction).normalized;
                    int     layer  = brain.GetDamageableLayerMask().value;

                    brain.UnitCombatController.FireProjectile(
                        origin, dir, brain.gameObject,
                        spawn.Speed, spawn.Damage, spawn.MaxDistance,
                        layer,
                        spawn.CritChance, spawn.CritMultiplier,
                        spawn.PierceChance, spawn.PierceMaxTargets);

                    DebugDraw.Ray(origin, dir * 1.2f, Color.red);
                }

                SystemAPI.SetComponentEnabled<EcsProjectileSpawnRequest>(entity, false);
            }
        }
    }
}