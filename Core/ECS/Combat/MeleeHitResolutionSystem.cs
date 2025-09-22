using OneBitRob.Core;
using OneBitRob.Debugging;
using OneBitRob.ECS;
using OneBitRob.FX;
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

            foreach (var (reqRW, attacker) in SystemAPI.Query<RefRW<MeleeHitRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<MeleeHitRequest>(attacker)) continue;

                var req = reqRW.ValueRO;
                var attackerBrain = UnitBrainRegistry.Get(attacker);
                if (attackerBrain == null)
                {
                    SystemAPI.SetComponentEnabled<MeleeHitRequest>(attacker, false);
                    continue;
                }

#if UNITY_EDITOR
                DebugDrawVolume(req);
#endif
                int hitCount = TryGetHits(in req);
                if (hitCount > 0)
                    ProcessHits(em, attacker, attackerBrain, in req, hitCount);

                SystemAPI.SetComponentEnabled<MeleeHitRequest>(attacker, false);
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

            float3 forward   = math.normalizesafe(req.Forward);
            float  cosHalf   = math.cos(req.HalfAngleRad);
            float  cosHalfSq = cosHalf * cosHalf;
            float  rangeSq   = req.Range * req.Range;
            int    maxTargets   = math.max(1, req.MaxTargets);
            int    appliedCount = 0;

            var meleeDef = attackerBrain.UnitDefinition?.weapon as MeleeWeaponDefinition;

            for (int i = 0; i < hitCount; i++)
            {
                var col = s_SphereOverlapHits[i];
                if (!col) continue;

                if (attackerBrain && col.transform.root == attackerBrain.transform.root) continue;

                if (!ShouldAffectTarget(col, attackerIsEnemy, in req, forward, cosHalfSq, rangeSq, out var targetBrain, out var toDelta)) continue;

#if UNITY_EDITOR
                DebugDraw.Line((Vector3)req.Origin, targetBrain.transform.position, DebugPalette.MeleeArc);
                DebugDraw.Line((Vector3)req.Origin + Vector3.up * 0.03f,
                               targetBrain.transform.position + Vector3.up * 0.03f,
                               DebugPalette.ProjectilePath);
#endif

                ApplyDamageAndFX(in req, attackerBrain, targetBrain, ((Vector3)toDelta).normalized, meleeDef);

                if (++appliedCount >= maxTargets) break;
            }
        }

        private static bool ShouldAffectTarget(
            Collider col, bool attackerIsEnemy, in MeleeHitRequest req,
            float3 forward, float cosHalfSq, float rangeSq,
            out UnitBrain targetBrain, out float3 toDelta)
        {
            targetBrain = col.GetComponentInParent<UnitBrain>();
            if (targetBrain == null || targetBrain.Health == null || !targetBrain.IsTargetAlive())
            {
                toDelta = default;
                return false;
            }

            bool targetIsEnemy = targetBrain.UnitDefinition != null && targetBrain.UnitDefinition.isEnemy;
            if (attackerIsEnemy == targetIsEnemy)
            {
                toDelta = default;
                return false;
            }

            toDelta = (float3)targetBrain.transform.position - req.Origin;
            float toDistSq = math.lengthsq(toDelta);
            if (toDistSq > rangeSq) return false;

            float dot = math.dot(forward, toDelta);
            if (dot <= 0f) return false;
            if ((dot * dot) < (cosHalfSq * toDistSq)) return false;

            return true;
        }

        private static void ApplyDamageAndFX(in MeleeHitRequest req, UnitBrain attackerBrain, UnitBrain targetBrain, Vector3 impactDir, MeleeWeaponDefinition meleeDef)
        {
            bool  isCrit = (req.CritChance > 0f) && (UnityEngine.Random.value < req.CritChance);
            float dmg    = isCrit ? req.Damage * math.max(1f, req.CritMultiplier) : req.Damage;

            targetBrain.Health.Damage(dmg, attackerBrain.gameObject, 0f, req.Invincibility, impactDir);

            DamageNumbersManager.Popup(
                new DamageNumbersParams
                {
                    Kind     = isCrit ? DamagePopupKind.CritDamage : DamagePopupKind.Damage,
                    Follow   = targetBrain.transform,
                    Position = targetBrain.transform.position,
                    Amount   = dmg
                }
            );

            if (meleeDef != null && meleeDef.hitFeedback != null)
                FeedbackService.TryPlay(meleeDef.hitFeedback, targetBrain.transform, targetBrain.transform.position);
        }

#if UNITY_EDITOR
        private static void DebugDrawVolume(in MeleeHitRequest req)
        {
            DebugDraw.WireSphereXYZ(req.Origin, req.Range, DebugPalette.AttackRange);

            Vector3 origin = (Vector3)req.Origin;
            Vector3 fwd    = ((Vector3)req.Forward).normalized;

            DebugDraw.Ray(origin, fwd * (req.Range * 0.9f), DebugPalette.AttackRange);

            float deg    = degrees(req.HalfAngleRad);
            Quaternion l = Quaternion.AngleAxis(-deg, Vector3.up);
            Quaternion r = Quaternion.AngleAxis(deg,  Vector3.up);

            DebugDraw.Ray(origin, (l * fwd) * req.Range, DebugPalette.Warning);
            DebugDraw.Ray(origin, (r * fwd) * req.Range, DebugPalette.Warning);
        }
#endif
    }
}
