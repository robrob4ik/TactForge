using OneBitRob.AI;
using OneBitRob.FX;
using OneBitRob.VFX;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_SpellProjectileBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRW<SpellProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawnRequest.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(entity);
                if (!brain || brain.UnitCombatController == null)
                {
                    spawnRequest.ValueRW = default; // consume anyway to avoid repeats
                    continue;
                }

                // Resolve projectile id string
                var projId = VisualAssetRegistry.GetProjectileId(spawnRequest.ValueRO.ProjectileIdHash);
                if (string.IsNullOrEmpty(projId))
                {
                    spawnRequest.ValueRW = default;
                    continue;
                }

                // Fetch the per-target hit feedback from the caster's spell (first spell slot by design)
                FeedbackDefinition perHit = null;
                var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                if (spells != null && spells.Count > 0 && spells[0] != null)
                    perHit = spells[0].perTargetHitFeedback;

                var origin = (Vector3)spawnRequest.ValueRO.Origin;
                var dir    = ((Vector3)spawnRequest.ValueRO.Direction).normalized;
                int layer  = spawnRequest.ValueRO.LayerMask;

                brain.UnitCombatController.FireSpellProjectile(
                    projId,
                    origin,
                    dir,
                    brain.gameObject,
                    spawnRequest.ValueRO.Speed,
                    spawnRequest.ValueRO.Damage,
                    spawnRequest.ValueRO.MaxDistance,
                    layer,
                    spawnRequest.ValueRO.Radius,
                    spawnRequest.ValueRO.Pierce != 0,
                    perHit // <-- new
                );

                spawnRequest.ValueRW = default; // consume
            }
        }
    }
}
