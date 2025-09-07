using OneBitRob.ECS;
using OneBitRob.FX;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using UnityEngine;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AICastPhaseGroup))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))] 
    public partial struct WeaponAttackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private EntityQuery _attackRequestQuery;
        private EntityQuery _windupQuery;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup    = state.GetComponentLookup<LocalTransform>(true);
            _attackRequestQuery = state.GetEntityQuery(ComponentType.ReadWrite<AttackRequest>());
            _windupQuery        = state.GetEntityQuery(ComponentType.ReadWrite<AttackWindup>());

            state.RequireForUpdate(state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<AttackRequest>(), ComponentType.ReadOnly<AttackWindup>() }
            }));
        }

        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            ReleaseFinishedWindups(em, ref ecb, now);
            ConsumeNewRequests   (em, ref ecb, now);

            ecb.Playback(em);
            ecb.Dispose();
        }

        // Phase 1: when a windup finishes, actually apply the attack
        private void ReleaseFinishedWindups(EntityManager em, ref EntityCommandBuffer ecb, float now)
        {
            using var windupEntities = _windupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < windupEntities.Length; i++)
            {
                var e = windupEntities[i];
                var windup = em.GetComponentData<AttackWindup>(e);
                if (windup.Active == 0 || now < windup.ReleaseTime) continue;

                // If a spell is currently mid‑cast, postpone firing the weapon.
                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                {
                    // keep windup active until spell finishes
                    continue;
                }

                var brain = UnitBrainRegistry.Get(e);
                var weaponDef = brain?.UnitDefinition?.weapon;

                // If we can't act (dead/no transform), just clear windup and continue.
                if (brain?.UnitCombatController == null || !brain.UnitCombatController.IsAlive || !_transformLookup.HasComponent(e))
                {
                    windup.Active = 0;
                    ecb.SetComponent(e, windup);
                    continue;
                }

                var lt = _transformLookup[e];
                float3 selfPos = lt.Position;
                var rot = lt.Rotation;
                float3 forward = normalizesafe(mul(rot, new float3(0, 0, 1)));
                float3 up      = normalizesafe(mul(rot, new float3(0, 1, 0)));
                float3 right   = normalizesafe(mul(rot, new float3(1, 0, 0)));

                var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                if (weaponDef is RangedWeaponDefinition ranged)
                {
                    FireRanged(em, ref ecb, e, brain, in ranged, in stats, selfPos, forward, right, up, now);
                }
                // NOTE: melee releases happen immediately on request now (no windup path)
                // We intentionally do not route melee through windup to keep the logic explicit and simple.

                windup.Active = 0;
                ecb.SetComponent(e, windup);
            }
        }

        // Phase 2: new requests arriving this frame
        private void ConsumeNewRequests(EntityManager em, ref EntityCommandBuffer ecb, float now)
        {
            using var entities = _attackRequestQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var req = em.GetComponentData<AttackRequest>(e);
                if (req.HasValue == 0 || req.Target == Entity.Null)
                { Consume(ref ecb, e); continue; }

                // If a spell is mid‑cast, we do not start a new weapon action.
                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                { Consume(ref ecb, e); continue; }

                var brain  = UnitBrainRegistry.Get(e);
                var weapon = brain?.UnitDefinition?.weapon;
                var stats  = em.HasComponent<UnitRuntimeStats>(e)
                           ? em.GetComponentData<UnitRuntimeStats>(e)
                           : UnitRuntimeStats.Defaults;

                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime) { Consume(ref ecb, e); continue; } // ← hard gate

                if (!_transformLookup.HasComponent(e) || !em.HasComponent<LocalTransform>(req.Target))
                { Consume(ref ecb, e); continue; }

                var selfLT   = _transformLookup[e];
                var targetLT = em.GetComponentData<LocalTransform>(req.Target);

                bool isRanged = weapon is RangedWeaponDefinition;
                float baseRange = max(0.01f, weapon != null ? weapon.attackRange : 1.5f);
                float rangeMult = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;
                float effectiveRange = baseRange * max(0.0001f, rangeMult);

                if (math.distancesq(selfLT.Position, targetLT.Position) > (effectiveRange * effectiveRange) * 1.1f)
                { Consume(ref ecb, e); continue; }

                float3 forward = normalizesafe(mul(selfLT.Rotation, new float3(0, 0, 1)));

                if (weapon is MeleeWeaponDefinition melee)
                {
                    // Immediate melee hit this frame
                    var hit = BuildMeleeHitRequest(e, brain, in melee, in stats, selfLT.Position, forward);
                    ecb.SetOrAdd(em, e, hit);

                    // Anim + VFX
                    brain.UnitCombatController?.PlayMeleeAttack(melee.attackAnimations);
                    FeedbackService.TryPlay(melee.attackFeedback, brain.transform, (Vector3)selfLT.Position);

                    // NEW: hard gate via cooldown (this was missing before)
                    var newCd = ComputeAttackCooldown(in melee.attackCooldown, in melee.attackCooldownJitter, stats.MeleeAttackSpeedMult, e, now);
                    ecb.SetOrAdd(em, e, newCd);
                    brain.NextAllowedAttackTime = Time.time + (newCd.NextTime - now);

                    // NEW: movement lock for the swing window (prevents sliding/orbiting while striking)
                    float lockSeconds = max(0f, melee.swingLockSeconds);
                    if (lockSeconds > 0f)
                    {
                        if (!em.HasComponent<ActionLockUntil>(e))
                            ecb.AddComponent(e, new ActionLockUntil { Until = now + lockSeconds });
                        else
                            ecb.SetComponent(e, new ActionLockUntil { Until = now + lockSeconds });
                    }
                }
                else if (weapon is RangedWeaponDefinition ranged)
                {
                    // Start windup (actual fire happens in Phase 1)
                    StartRangedWindup(em, ref ecb, e, brain, in ranged, in stats, selfLT.Position, forward, now);
                }

                Consume(ref ecb, e);
            }
        }

        // RANGED helpers
        private void FireRanged(EntityManager em, ref EntityCommandBuffer ecb, Entity e, UnitBrain brain,
                                in RangedWeaponDefinition ranged, in UnitRuntimeStats stats,
                                float3 selfPos, float3 forward, float3 right, float3 up, float now)
        {
            float3 origin = selfPos
                          + forward * max(0f, ranged.muzzleForward)
                          + right   * ranged.muzzleLocalOffset.x
                          + up      * ranged.muzzleLocalOffset.y
                          + forward * ranged.muzzleLocalOffset.z;

            float3 aimDir = forward;
            if (em.HasComponent<Target>(e))
            {
                var targetEnt = em.GetComponentData<Target>(e).Value;
                if (targetEnt != Entity.Null && em.HasComponent<LocalTransform>(targetEnt))
                {
                    float3 targetPos = em.GetComponentData<LocalTransform>(targetEnt).Position;
                    float3 raw = targetPos - origin; raw.y = 0;
                    aimDir = normalizesafe(raw, forward);
                }
            }

            // Final crit/pierce
            float critChance   = clamp(ranged.critChance + stats.CritChanceAdd, 0f, 1f);
            float critMult     = max(1f, ranged.critMultiplier * stats.CritMultiplierMult);
            float pierceChance = clamp(stats.RangedPierceChanceAdd, 0f, 1f);
            int   pierceMax    = max(0,   stats.RangedPierceMaxAdd);

            var spawn = new EcsProjectileSpawnRequest
            {
                Origin           = origin,
                Direction        = aimDir,
                Speed            = max(0.01f, ranged.projectileSpeed),
                Damage           = max(0f, ranged.attackDamage),
                MaxDistance      = max(0.1f, ranged.projectileMaxDistance),
                CritChance       = critChance,
                CritMultiplier   = critMult,
                PierceChance     = pierceChance,
                PierceMaxTargets = pierceMax,
                HasValue         = 1
            };
            ecb.SetOrAdd(em, e, spawn);

            brain.UnitCombatController?.PlayRangedFire(ranged.animations);
            FeedbackService.TryPlay(ranged.fireFeedback, brain.transform, (Vector3)origin);

#if UNITY_EDITOR
            Debug.DrawRay((Vector3)origin, (Vector3)aimDir * 1.6f, new Color(1f, 0.45f, 0.2f, 0.95f), 0.55f, false);
#endif

            var cd = ComputeAttackCooldown(in ranged.attackCooldown, in ranged.attackCooldownJitter, stats.RangedAttackSpeedMult, e, now);
            ecb.SetOrAdd(em, e, cd);
            brain.NextAllowedAttackTime = Time.time + (cd.NextTime - now);
        }

        private void StartRangedWindup(EntityManager em, ref EntityCommandBuffer ecb, Entity e, UnitBrain brain,
                                       in RangedWeaponDefinition ranged, in UnitRuntimeStats stats,
                                       float3 selfPos, float3 forward, float now)
        {
            if (!em.HasComponent<AttackWindup>(e)) ecb.AddComponent(e, new AttackWindup { Active = 0, ReleaseTime = 0 });

            var wind = em.HasComponent<AttackWindup>(e) ? em.GetComponentData<AttackWindup>(e) : default;
            if (wind.Active != 0) return;

            float speedMult = max(0.0001f, stats.RangedAttackSpeedMult);
            wind.Active      = 1;
            wind.ReleaseTime = now + max(0f, ranged.windupSeconds) / speedMult;
            ecb.SetComponent(e, wind);

            brain.UnitCombatController?.PlayRangedPrepare(ranged.animations);
            FeedbackService.TryPlay(ranged.prepareFeedback, brain.transform, (Vector3)selfPos);

#if UNITY_EDITOR
            Debug.DrawRay((Vector3)selfPos + Vector3.up * 0.05f, (Vector3)forward * 1.2f, new Color(0.8f, 0.9f, 1f, 0.95f), 0.65f, false);
#endif
        }

        // MELEE helpers
        private static MeleeHitRequest BuildMeleeHitRequest(Entity e, UnitBrain brain, in MeleeWeaponDefinition melee, in UnitRuntimeStats stats, float3 pos, float3 forward)
        {
            return new MeleeHitRequest
            {
                Origin        = pos,
                Forward       = forward,
                Range         = max(0.01f, melee.attackRange * max(0.0001f, stats.MeleeRangeMult)),
                HalfAngleRad  = radians(clamp(melee.halfAngleDeg * max(0.0001f, stats.MeleeArcMult), 0f, 179f)),
                Damage        = max(1f, melee.attackDamage),
                Invincibility = max(0f, melee.invincibility),
                LayerMask     = (UnitBrainRegistry.Get(e)?.GetDamageableLayerMask().value) ?? ~0,
                MaxTargets    = max(1, melee.maxTargets),
                CritChance    = clamp(melee.critChance + stats.CritChanceAdd, 0f, 1f),
                CritMultiplier= max(1f, melee.critMultiplier * stats.CritMultiplierMult),
                HasValue      = 1
            };
        }

        private static void Consume(ref EntityCommandBuffer ecb, Entity e)
            => ecb.SetComponent(e, new AttackRequest { HasValue = 0, Target = Entity.Null });

        private static AttackCooldown ComputeAttackCooldown(in float baseCd, in float jitterRange, float speedMult, Entity e, float now)
        {
            float jitter = CalcJitter(jitterRange, e, now);
            speedMult    = max(0.0001f, speedMult);
            return new AttackCooldown { NextTime = now + max(0.01f, baseCd) / speedMult + jitter };
        }

        private static float CalcJitter(float range, Entity e, float now)
        {
            if (range <= 0f) return 0f;
            uint h = math.hash(new float3(now, e.Index, e.Version));
            float u = (h / (float)uint.MaxValue) * 2f - 1f;
            return u * range;
        }
    }
}
