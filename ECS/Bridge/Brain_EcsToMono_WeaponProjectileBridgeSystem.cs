// ECS/HybridSync/EcsToMono/Brain_EcsToMono_WeaponProjectileBridgeSystem.cs
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// Consumes EcsProjectileSpawnRequest and fires Mono projectile once.
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    [UpdateAfter(typeof(OneBitRob.AI.WeaponAttackSystem))] // requests set there
    public partial struct Brain_EcsToMono_WeaponProjectileBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (spawn, e) in SystemAPI.Query<RefRW<EcsProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawn.ValueRO.HasValue == 0) continue;

                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (brain && brain.CombatSubsystem != null)
                {
                    var origin = (Vector3)spawn.ValueRO.Origin;
                    var dir    = ((Vector3)spawn.ValueRO.Direction).normalized;
                    int layer  = brain.GetDamageableLayerMask().value;

                    brain.CombatSubsystem.FireProjectile(
                        origin, dir, brain.gameObject,
                        spawn.ValueRO.Speed, spawn.ValueRO.Damage, spawn.ValueRO.MaxDistance,
                        layer, spawn.ValueRO.CritChance, spawn.ValueRO.CritMultiplier);

#if UNITY_EDITOR
                    Debug.DrawRay(origin, dir * 1.2f, Color.red, 0f, false);
#endif
                }
                spawn.ValueRW = default; // consume
            }
        }
    }
}