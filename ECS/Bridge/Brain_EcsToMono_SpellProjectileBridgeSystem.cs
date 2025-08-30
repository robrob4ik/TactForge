using OneBitRob.AI;
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
            foreach (var (spawn, e) in SystemAPI.Query<RefRW<SpellProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawn.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain && brain.CombatSubsystem != null)
                {
                    var origin = (Vector3)spawn.ValueRO.Origin;
                    var dir    = ((Vector3)spawn.ValueRO.Direction).normalized;
                    string projId = SpellVisualRegistry.GetProjectileId(spawn.ValueRO.ProjectileIdHash);

                    brain.CombatSubsystem.FireSpellProjectile(
                        projId, origin, dir, brain.gameObject,
                        spawn.ValueRO.Speed, spawn.ValueRO.Damage, spawn.ValueRO.MaxDistance,
                        spawn.ValueRO.LayerMask, spawn.ValueRO.Radius, spawn.ValueRO.Pierce == 1
                        );

#if UNITY_EDITOR
                    Debug.DrawRay(origin, dir * 1.2f, Color.magenta, 0f, false);
#endif
                }
                spawn.ValueRW = default; // consume
            }
        }
    }
}