using OneBitRob.ECS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using OneBitRob.FX;

namespace OneBitRob.AI
{
    /// One-shot, hybrid melee hitscan:
    /// - single Physics.OverlapSphereNonAlloc per request
    /// - cone test + cap targets, then applies EnigmaHealth.Damage on the Mono side
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct MeleeHitResolutionSystem : ISystem
    {
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

                // consume first
                req.HasValue = 0;
                em.SetComponentData(e, req);

                var attackerBrain = UnitBrainRegistry.Get(e);
                if (attackerBrain == null)
                    continue;

                // ────────────── collect candidates ──────────────
                int maskToUse = (req.LayerMask != 0) ? req.LayerMask : ~0;
                int count = Physics.OverlapSphereNonAlloc(
                    (Vector3)req.Origin,
                    req.Range,
                    s_Hits,
                    maskToUse,
                    QueryTriggerInteraction.Collide
                );

                // Fallback: if mask filtering produced no hits, re-run across all layers
                if (count == 0 && maskToUse != ~0)
                {
                    count = Physics.OverlapSphereNonAlloc(
                        (Vector3)req.Origin,
                        req.Range,
                        s_Hits,
                        ~0,
                        QueryTriggerInteraction.Collide
                    );
                }

#if UNITY_EDITOR
                // visualize the attack volume each time a request is processed
                DebugDrawVolume(in req);
#endif

                if (count <= 0)
                    continue;

                bool attackerIsEnemy = attackerBrain.UnitDefinition != null && attackerBrain.UnitDefinition.isEnemy;

                float3 fwd = math.normalizesafe(req.Forward);
                float cosHalf = math.cos(req.HalfAngleRad);
                float cos2 = cosHalf * cosHalf;
                float rangeSq = req.Range * req.Range;
                int maxT = math.max(1, req.MaxTargets);
                int applied = 0;

                for (int h = 0; h < count; h++)
                {
                    var col = s_Hits[h];
                    if (!col) continue;

                    // ignore own root
                    if (attackerBrain && col.transform.root == attackerBrain.transform.root)
                        continue;

                    var targetBrain = col.GetComponentInParent<UnitBrain>();
                    if (targetBrain == null || targetBrain.Health == null || !targetBrain.IsTargetAlive())
                        continue;

                    // 🔒 no friendly fire
                    bool targetIsEnemy = targetBrain.UnitDefinition != null && targetBrain.UnitDefinition.isEnemy;
                    if (attackerIsEnemy == targetIsEnemy)
                        continue;

                    float3 to = (float3)targetBrain.transform.position - req.Origin;
                    float sq = math.lengthsq(to);
                    if (sq > rangeSq) continue;

                    float dot = math.dot(fwd, to);
                    if (dot <= 0f) continue;                 // behind us
                    if ((dot * dot) < (cos2 * sq)) continue; // outside cone

#if UNITY_EDITOR
                    // show "candidate" line (green)
                    Debug.DrawLine((Vector3)req.Origin, targetBrain.transform.position, new Color(0.2f, 1f, 0.2f, 0.9f), 0.1f, false);
#endif

                    // Let Health handle invincibility internally; do not pre-gate.
                    bool isCrit = (req.CritChance > 0f) && (UnityEngine.Random.value < req.CritChance);
                    float dmg = isCrit ? req.Damage * math.max(1f, req.CritMultiplier) : req.Damage;

                    Vector3 dir = ((Vector3)to).normalized;
                    targetBrain.Health.Damage(dmg, attackerBrain.gameObject, 0f, req.Invincibility, dir);

                    DamageNumbersManager.Popup(new DamageNumbersParams
                    {
                        Kind     = isCrit ? DamagePopupKind.CritDamage : DamagePopupKind.Damage,
                        Follow   = targetBrain.transform,
                        Position = targetBrain.transform.position,
                        Amount   = dmg
                    });

#if UNITY_EDITOR
                    // show "actual hit" line (yellow) slightly thicker via double draw
                    Debug.DrawLine((Vector3)req.Origin, targetBrain.transform.position, new Color(1f, 0.95f, 0.2f, 1f), 0.12f, false);
                    Debug.DrawLine((Vector3)req.Origin + Vector3.up * 0.03f, targetBrain.transform.position + Vector3.up * 0.03f, new Color(1f, 0.95f, 0.2f, 1f), 0.12f, false);
#endif

                    if (++applied >= maxT)
                        break;
                }
            }

            entities.Dispose();
        }

#if UNITY_EDITOR
        private static void DebugDrawVolume(in MeleeHitRequest req)
        {
            // Range sphere
            Color sphereColor = new Color(1f, 0f, 0f, 0.25f);
            DrawWireSphere(req.Origin, req.Range, sphereColor, 0.11f);

            // Forward direction
            Vector3 origin = (Vector3)req.Origin;
            Vector3 fwd = ((Vector3)req.Forward).normalized;
            Debug.DrawRay(origin, fwd * (req.Range * 0.9f), Color.red, 0.11f, false);

            // Cone edges (approx)
            float deg = math.degrees(req.HalfAngleRad);
            Quaternion left  = Quaternion.AngleAxis(-deg, Vector3.up);
            Quaternion right = Quaternion.AngleAxis( deg, Vector3.up);
            Debug.DrawRay(origin, (left  * fwd) * req.Range, new Color(1f, 0.5f, 0.2f, 0.9f), 0.11f, false);
            Debug.DrawRay(origin, (right * fwd) * req.Range, new Color(1f, 0.5f, 0.2f, 0.9f), 0.11f, false);
        }

        private static void DrawWireSphere(float3 center, float radius, Color color, float dur)
        {
            // minimal wire sphere using 3 great circles
            const int segments = 24;
            Vector3 c = (Vector3)center;
            Vector3 prev = c + Vector3.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                // XY
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
                Debug.DrawLine(prev, p, color, dur, false);
                prev = p;
            }
            prev = c + Vector3.forward * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                // XZ
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                Debug.DrawLine(prev, p, color, dur, false);
                prev = p;
            }
            prev = c + Vector3.up * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                // YZ
                Vector3 p = c + new Vector3(0f, Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
                Debug.DrawLine(prev, p, color, dur, false);
                prev = p;
            }
        }
#endif
    }
}
