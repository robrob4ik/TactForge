using OneBitRob.Debugging;
using OneBitRob.ECS;
using OneBitRob.FX;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct MeleeHitResolutionSystem : ISystem
    {
        private static readonly Collider[] s_SphereOverlapHits = new Collider[256];
        private EntityQuery _requestQuery;

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = state.GetEntityQuery(ComponentType.ReadWrite<MeleeHitRequest>());
            state.RequireForUpdate(_requestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            var requestEntities = _requestQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < requestEntities.Length; i++)
            {
                var entity = requestEntities[i];
                var request = em.GetComponentData<MeleeHitRequest>(entity);
                if (request.HasValue == 0)
                    continue;

                // consume first
                request.HasValue = 0;
                em.SetComponentData(entity, request);

                var attackerBrain = UnitBrainRegistry.Get(entity);
                if (attackerBrain == null)
                    continue;

                int maskToUse = (request.LayerMask != 0) ? request.LayerMask : ~0;
                int hitCount = Physics.OverlapSphereNonAlloc(
                    (Vector3)request.Origin,
                    request.Range,
                    s_SphereOverlapHits,
                    maskToUse,
                    QueryTriggerInteraction.Collide
                );

                // Fallback: if mask filtering produced no hits, re-run across all layers
                if (hitCount == 0 && maskToUse != ~0)
                {
                    hitCount = Physics.OverlapSphereNonAlloc(
                        (Vector3)request.Origin,
                        request.Range,
                        s_SphereOverlapHits,
                        ~0,
                        QueryTriggerInteraction.Collide
                    );
                }

                DebugDrawVolume(in request);


                if (hitCount <= 0)
                    continue;

                bool attackerIsEnemy = attackerBrain.UnitDefinition != null && attackerBrain.UnitDefinition.isEnemy;

                float3 forward = normalizesafe(request.Forward);
                float cosHalf = cos(request.HalfAngleRad);
                float cosHalfSq = cosHalf * cosHalf;
                float rangeSq = request.Range * request.Range;
                int maxTargets = max(1, request.MaxTargets);
                int applied = 0;

                var meleeDef = attackerBrain.UnitDefinition?.weapon as OneBitRob.MeleeWeaponDefinition;

                for (int h = 0; h < hitCount; h++)
                {
                    var col = s_SphereOverlapHits[h];
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

                    float3 to = (float3)targetBrain.transform.position - request.Origin;
                    float sq = lengthsq(to);
                    if (sq > rangeSq) continue;

                    float dot = math.dot(forward, to);
                    if (dot <= 0f) continue;                 // behind us
                    if ((dot * dot) < (cosHalfSq * sq)) continue; // outside cone

                    DebugDraw.Line((Vector3)request.Origin, targetBrain.transform.position, new Color(0.2f, 1f, 0.2f, 0.95f));


                    // Let Health handle invincibility internally; do not pre-gate.
                    bool isCrit = (request.CritChance > 0f) && (UnityEngine.Random.value < request.CritChance);
                    float damage = isCrit ? request.Damage * max(1f, request.CritMultiplier) : request.Damage;

                    Vector3 impactDir = ((Vector3)to).normalized;
                    targetBrain.Health.Damage(damage, attackerBrain.gameObject, 0f, request.Invincibility, impactDir);

                    DamageNumbersManager.Popup(new DamageNumbersParams
                    {
                        Kind     = isCrit ? DamagePopupKind.CritDamage : DamagePopupKind.Damage,
                        Follow   = targetBrain.transform,
                        Position = targetBrain.transform.position,
                        Amount   = damage
                    });

                    if (meleeDef != null && meleeDef.hitFeedback != null)
                    {
                        // Attach to target so it follows if the target staggers/moves
                        FeedbackService.TryPlay(meleeDef.hitFeedback, targetBrain.transform, targetBrain.transform.position);
                    }

                    // "Thicker" highlight: second line slightly offset
                    DebugDraw.Line((Vector3)request.Origin + Vector3.up * 0.03f,
                        targetBrain.transform.position + Vector3.up * 0.03f,
                        new Color(1f, 0.95f, 0.2f, 1f));
                    
                    if (++applied >= maxTargets)
                        break;
                }
            }

            requestEntities.Dispose();
        }

#if UNITY_EDITOR
        private static void DebugDrawVolume(in MeleeHitRequest req)
        {
            // Range sphere
            Color sphereColor = new Color(1f, 0f, 0f, 0.28f);
            DrawWireSphere(req.Origin, req.Range, sphereColor, 0.25f);

            // Forward direction
            Vector3 origin = (Vector3)req.Origin;
            Vector3 fwd = ((Vector3)req.Forward).normalized;
            DebugDraw.Ray(origin, fwd * (req.Range * 0.9f), Color.red);

            // Cone edges (approx)
            float deg = degrees(req.HalfAngleRad);
            Quaternion left  = Quaternion.AngleAxis(-deg, Vector3.up);
            Quaternion right = Quaternion.AngleAxis( deg, Vector3.up);
            DebugDraw.Ray(origin, (left  * fwd) * req.Range, new Color(1f, 0.6f, 0.2f, 0.95f));
            DebugDraw.Ray(origin, (right * fwd) * req.Range, new Color(1f, 0.6f, 0.2f, 0.95f));
        }

        private static void DrawWireSphere(float3 center, float radius, Color color, float duration)
        {
            const int segments = 24;
            Vector3 c = (Vector3)center;
            Vector3 prev = c + Vector3.right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                // XY
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
                DebugDraw.Line(prev, p, color);
                prev = p;
            }
            prev = c + Vector3.forward * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                // XZ
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                DebugDraw.Line(prev, p, color);
                prev = p;
            }
            prev = c + Vector3.up * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                // YZ
                Vector3 p = c + new Vector3(0f, Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
                DebugDraw.Line(prev, p, color);
                prev = p;
            }
        }
#endif
    }
}
