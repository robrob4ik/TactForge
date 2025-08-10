using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.AI
{
    /// One-shot, hybrid melee hitscan:
    /// - single Physics.OverlapSphereNonAlloc per request
    /// - cone test + cap targets, then applies EnigmaHealth.Damage on the Mono side
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct MeleeHitResolutionSystem : ISystem
    {
        // Increase if crowds get very dense.
        private static readonly Collider[] s_Hits = new Collider[256];

        private EntityQuery _reqQuery;

        public void OnCreate(ref SystemState state)
        {
            _reqQuery = state.GetEntityQuery(ComponentType.ReadWrite<MeleeHitRequest>());
            state.RequireForUpdate(_reqQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            var entities = _reqQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var req = em.GetComponentData<MeleeHitRequest>(e);
                if (req.HasValue == 0)
                    continue;

                // Clear first to avoid reprocessing
                req.HasValue = 0;
                em.SetComponentData(e, req);

                var attackerBrain = UnitBrainRegistry.Get(e);
                if (attackerBrain == null)
                    continue;

                // Physics query
                int count = Physics.OverlapSphereNonAlloc(
                    (Vector3)req.Origin,
                    req.Range,
                    s_Hits,
                    req.LayerMask
                );
                if (count <= 0)
                    continue;

                float3 fwd = math.normalizesafe(req.Forward);
                float  cosHalf = math.cos(req.HalfAngleRad);
                float  cos2 = cosHalf * cosHalf;
                float  rangeSq = req.Range * req.Range;
                int    maxT = math.max(1, req.MaxTargets);
                int    applied = 0;

                for (int h = 0; h < count; h++)
                {
                    var col = s_Hits[h];
                    if (!col) continue;

                    // Don't hit the shooter root
                    if (attackerBrain && col.transform.root == attackerBrain.transform.root)
                        continue;

                    var targetBrain = col.GetComponentInParent<UnitBrain>();
                    if (targetBrain == null || targetBrain.Health == null || !targetBrain.IsTargetAlive())
                        continue;

                    float3 to = (float3)targetBrain.transform.position - req.Origin;
                    float  sq = math.lengthsq(to);
                    if (sq > rangeSq) continue;

                    float dot = math.dot(fwd, to);
                    if (dot <= 0f) continue;                  // behind
                    if ((dot * dot) < (cos2 * sq)) continue;  // outside cone

                    // Apply damage
                    if (targetBrain.Health.CanTakeDamageThisFrame())
                    {
                        Vector3 dir = ((Vector3)to).normalized;
                        targetBrain.Health.Damage(req.Damage, attackerBrain.gameObject, 0f, req.Invincibility, dir);

                        if (++applied >= maxT)
                            break;
                    }
                }
            }

            entities.Dispose();
        }
    }
}
