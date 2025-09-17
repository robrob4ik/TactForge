// File: OneBitRob/AI/MeleeHitResolutionSystem.cs

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
    [UpdateInGroup(typeof(AIResolvePhaseGroup))]
    public partial struct MeleeHitResolutionSystem : ISystem
    {
        private static readonly Collider[] s_SphereOverlapHits = new Collider[256];

        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            foreach (var (reqRW, e) in SystemAPI.Query<RefRW<MeleeHitRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<MeleeHitRequest>(e)) continue;
                var req = reqRW.ValueRO;

                var brain = UnitBrainRegistry.Get(e);
                if (brain == null) { SystemAPI.SetComponentEnabled<MeleeHitRequest>(e, false); continue; }

                DebugDrawVolume(req);

                int hitCount = TryGetHits(in req);
                if (hitCount > 0) ProcessHits(em, e, brain, in req, hitCount);

                SystemAPI.SetComponentEnabled<MeleeHitRequest>(e, false);
            }
        }

        private static int TryGetHits(in MeleeHitRequest req)
        {
            int maskToUse = (req.LayerMask != 0) ? req.LayerMask : ~0;
            int count = Physics.OverlapSphereNonAlloc((Vector3)req.Origin, req.Range, s_SphereOverlapHits, maskToUse, QueryTriggerInteraction.Collide);
            if (count == 0 && maskToUse != ~0)
                count = Physics.OverlapSphereNonAlloc((Vector3)req.Origin, req.Range, s_SphereOverlapHits, ~0, QueryTriggerInteraction.Collide);
            return count;
        }

        private static void ProcessHits(EntityManager em, Entity attacker, UnitBrain attackerBrain, in MeleeHitRequest req, int hitCount)
        {
            bool attackerIsEnemy = attackerBrain.UnitDefinition != null && attackerBrain.UnitDefinition.isEnemy;

            float3 forward = math.normalizesafe(req.Forward);
            float cosHalf  = math.cos(req.HalfAngleRad);
            float cosHalfSq= cosHalf * cosHalf;
            float rangeSq  = req.Range * req.Range;
            int   maxT     = math.max(1, req.MaxTargets);
            int   applied  = 0;

            var meleeDef = attackerBrain.UnitDefinition?.weapon as OneBitRob.MeleeWeaponDefinition;

            for (int i = 0; i < hitCount; i++)
            {
                var col = s_SphereOverlapHits[i];
                if (!col) continue;

                if (attackerBrain && col.transform.root == attackerBrain.transform.root)
                    continue;

                if (!ShouldAffectTarget(col, attackerIsEnemy, in req, forward, cosHalfSq, rangeSq, out var targetBrain, out var to))
                    continue;

                DebugDraw.Line((Vector3)req.Origin, targetBrain.transform.position, new Color(0.2f, 1f, 0.2f, 0.95f));
                DebugDraw.Line((Vector3)req.Origin + Vector3.up * 0.03f, targetBrain.transform.position + Vector3.up * 0.03f, new Color(1f, 0.95f, 0.2f, 1f));

                ApplyDamageAndFX(in req, attackerBrain, targetBrain, ((Vector3)to).normalized, meleeDef);

                if (++applied >= maxT) break;
            }
        }

        private static bool ShouldAffectTarget(Collider col, bool attackerIsEnemy, in MeleeHitRequest req,
                                               float3 forward, float cosHalfSq, float rangeSq,
                                               out UnitBrain targetBrain, out float3 to)
        {
            targetBrain = col.GetComponentInParent<UnitBrain>();
            if (targetBrain == null || targetBrain.Health == null || !targetBrain.IsTargetAlive())
            { to = default; return false; }

            bool targetIsEnemy = targetBrain.UnitDefinition != null && targetBrain.UnitDefinition.isEnemy;
            if (attackerIsEnemy == targetIsEnemy) { to = default; return false; }

            to = (float3)targetBrain.transform.position - req.Origin;
            float sq = math.lengthsq(to);
            if (sq > rangeSq) return false;

            float dot = math.dot(forward, to);
            if (dot <= 0f) return false;
            if ((dot * dot) < (cosHalfSq * sq)) return false;

            return true;
        }

        private static void ApplyDamageAndFX(in MeleeHitRequest req, UnitBrain attackerBrain, UnitBrain targetBrain, Vector3 impactDir, OneBitRob.MeleeWeaponDefinition meleeDef)
        {
            bool  isCrit = (req.CritChance > 0f) && (UnityEngine.Random.value < req.CritChance);
            float dmg    = isCrit ? req.Damage * math.max(1f, req.CritMultiplier) : req.Damage;

            targetBrain.Health.Damage(dmg, attackerBrain.gameObject, 0f, req.Invincibility, impactDir);

            DamageNumbersManager.Popup(new DamageNumbersParams
            {
                Kind     = isCrit ? DamagePopupKind.CritDamage : DamagePopupKind.Damage,
                Follow   = targetBrain.transform,
                Position = targetBrain.transform.position,
                Amount   = dmg
            });

            if (meleeDef != null && meleeDef.hitFeedback != null)
                FeedbackService.TryPlay(meleeDef.hitFeedback, targetBrain.transform, targetBrain.transform.position);
        }

#if UNITY_EDITOR
        private static void DebugDrawVolume(in MeleeHitRequest req)
        {
            Color sphereColor = new Color(1f, 0f, 0f, 0.28f);
            DrawWireSphere(req.Origin, req.Range, sphereColor, 0.25f);

            Vector3 origin = (Vector3)req.Origin;
            Vector3 fwd = ((Vector3)req.Forward).normalized;
            DebugDraw.Ray(origin, fwd * (req.Range * 0.9f), Color.red);

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
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
                DebugDraw.Line(prev, p, color);
                prev = p;
            }
            prev = c + Vector3.forward * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                DebugDraw.Line(prev, p, color);
                prev = p;
            }
            prev = c + Vector3.up * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f * Mathf.PI;
                Vector3 p = c + new Vector3(0f, Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
                DebugDraw.Line(prev, p, color);
                prev = p;
            }
        }
#endif
    }
}
