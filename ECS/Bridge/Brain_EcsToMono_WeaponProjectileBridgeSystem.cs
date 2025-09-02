using OneBitRob.AI;
using OneBitRob.Debugging;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    [UpdateAfter(typeof(WeaponAttackSystem))]
    public partial struct Brain_EcsToMono_WeaponProjectileBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRW<EcsProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawnRequest.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(entity);
                if (brain && brain.UnitCombatController != null)
                {
                    var origin = (Vector3)spawnRequest.ValueRO.Origin;
                    var dir    = ((Vector3)spawnRequest.ValueRO.Direction).normalized;
                    int layer  = brain.GetDamageableLayerMask().value;

                    brain.UnitCombatController.FireProjectile(
                        origin, dir, brain.gameObject,
                        spawnRequest.ValueRO.Speed, spawnRequest.ValueRO.Damage, spawnRequest.ValueRO.MaxDistance,
                        layer,
                        spawnRequest.ValueRO.CritChance, spawnRequest.ValueRO.CritMultiplier,
                        spawnRequest.ValueRO.PierceChance, spawnRequest.ValueRO.PierceMaxTargets);

                    DebugDraw.Ray(origin, dir * 1.2f, Color.red);
                }
                spawnRequest.ValueRW = default; // consume
            }
        }
    }
}