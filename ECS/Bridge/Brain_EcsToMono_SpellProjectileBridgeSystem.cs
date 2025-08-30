using OneBitRob.AI;
using OneBitRob.Debugging;
using OneBitRob.VFX;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial struct Brain_EcsToMono_SpellProjectileBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRW<SpellProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawnRequest.ValueRO.HasValue == 0) continue;

                var brain = OneBitRob.AI.UnitBrainRegistry.Get(entity);
                if (brain && brain.UnitCombatController != null)
                {
                    var origin = (Vector3)spawnRequest.ValueRO.Origin;
                    var dir    = ((Vector3)spawnRequest.ValueRO.Direction).normalized;
                    string projId = VisualAssetRegistry.GetProjectileId(spawnRequest.ValueRO.ProjectileIdHash);

                    brain.UnitCombatController.FireSpellProjectile(
                        projId, origin, dir, brain.gameObject,
                        spawnRequest.ValueRO.Speed, spawnRequest.ValueRO.Damage, spawnRequest.ValueRO.MaxDistance,
                        spawnRequest.ValueRO.LayerMask, spawnRequest.ValueRO.Radius, spawnRequest.ValueRO.Pierce == 1
                    );

                    DebugDraw.Ray(origin, dir * 1.2f, Color.magenta);
                }
                spawnRequest.ValueRW = default; // consume
            }
        }
    }
}