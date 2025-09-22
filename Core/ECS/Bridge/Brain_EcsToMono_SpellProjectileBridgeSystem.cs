using OneBitRob.AI;
using Unity.Entities;
using UnityEngine;
using OneBitRob.FX;
using OneBitRob.VFX;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_SpellProjectileBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            foreach (var (spawnRW, entity) in SystemAPI.Query<RefRW<SpellProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<SpellProjectileSpawnRequest>(entity)) continue;
                var spawnRequest = spawnRW.ValueRO;

                var brain = UnitBrainRegistry.Get(entity);
                if (brain && brain.UnitCombatController != null)
                {
                    var projId = VisualAssetRegistry.GetProjectileId(spawnRequest.ProjectileIdHash);
                    if (!string.IsNullOrEmpty(projId))
                    {
                        var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                        FeedbackDefinition perHit = null;
                        if (spells != null && spells.Count > 0 && spells[0] != null)
                            perHit = spells[0].perTargetHitFeedback;

                        brain.UnitCombatController.FireSpellProjectile(
                            projId,
                            (Vector3)spawnRequest.Origin,
                            ((Vector3)spawnRequest.Direction).normalized,
                            brain.gameObject,
                            spawnRequest.Speed,
                            spawnRequest.Damage,
                            spawnRequest.MaxDistance,
                            spawnRequest.LayerMask,
                            spawnRequest.Radius,
                            spawnRequest.Pierce != 0,
                            perHit
                        );
                    }
                }

                SystemAPI.SetComponentEnabled<SpellProjectileSpawnRequest>(entity, false);
            }
        }
    }
}
