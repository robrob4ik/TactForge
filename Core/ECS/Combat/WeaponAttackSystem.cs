// File: OneBitRob/AI/WeaponAttackSystem.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.Core;
using OneBitRob.ECS;
using OneBitRob.FX;
using PROJECT.Scripts.AI.Brain.OneBitRob.Core;
using static Unity.Mathematics.math;
using UnityEngine;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AICastPhaseGroup))]
    public partial struct WeaponAttackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private EntityQuery _attackRequestQuery;
        private EntityQuery _windupQuery;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);

            _attackRequestQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadWrite<AttackRequest>() },
                None = new[] { ComponentType.ReadOnly<DestroyEntityTag>() }
            });

            _windupQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadWrite<AttackWindup>() },
                None = new[] { ComponentType.ReadOnly<DestroyEntityTag>() }
            });

            state.RequireForUpdate(state.GetEntityQuery(new EntityQueryDesc
            {
                Any  = new[] { ComponentType.ReadOnly<AttackRequest>(), ComponentType.ReadOnly<AttackWindup>() },
                None = new[] { ComponentType.ReadOnly<DestroyEntityTag>() }
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
                if (em.HasComponent<DestroyEntityTag>(e)) continue;

                var windup = em.GetComponentData<AttackWindup>(e);
                if (windup.Active == 0 || now < windup.ReleaseTime) continue;

                // If a spell is currently mid‑cast, postpone firing the weapon.
                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                    continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain == null || brain.UnitCombatController == null || !brain.UnitCombatController.IsAlive || !_transformLookup.HasComponent(e))
                {
                    windup.Active = 0;
                    ecb.SetComponent(e, windup);
                    continue;
                }

                var lt    = _transformLookup[e];
                var pose  = AttackPose.FromLocalTransform(lt);
                var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;
                var uw    = em.HasComponent<UnitWeaponStatic>(e)  ? em.GetComponentData<UnitWeaponStatic>(e) : default;
                bool hasUws = em.HasComponent<UnitWeaponStatic>(e);

                // If ranged, actually fire and play fire anim/FX once here
                bool isRanged = false;
                if (em.HasComponent<UnitStatic>(e))
                {
                    isRanged = em.GetComponentData<UnitStatic>(e).CombatStyle == 2;
                }
                else
                {
                    isRanged = brain.UnitDefinition?.weapon is RangedWeaponDefinition;
                }

                if (isRanged)
                {
                    var rangedDef = brain.UnitDefinition?.weapon as RangedWeaponDefinition;
                    FireRanged(em, ref ecb, e, brain, in uw, hasUws, rangedDef, in stats, in pose, now);

                    if (rangedDef != null)
                    {
                        brain.UnitCombatController?.PlayRangedFireCompute();
                        // Place fire feedback at muzzle origin
                        float3 origin = pose.Position
                                      + pose.Forward * max(0f, hasUws ? uw.MuzzleForward : (rangedDef?.muzzleForward ?? 0f))
                                      + pose.Right   * (hasUws ? uw.MuzzleLocalOffset.x : (rangedDef?.muzzleLocalOffset.x ?? 0f))
                                      + pose.Up      * (hasUws ? uw.MuzzleLocalOffset.y : (rangedDef?.muzzleLocalOffset.y ?? 0f))
                                      + pose.Forward * (hasUws ? uw.MuzzleLocalOffset.z : (rangedDef?.muzzleLocalOffset.z ?? 0f));
                        if (rangedDef.fireFeedback != null)
                            FeedbackService.TryPlay(rangedDef.fireFeedback, brain.transform, (Vector3)origin);
                    }
                }

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

                if (em.HasComponent<DestroyEntityTag>(e))
                { Consume(ref ecb, e); continue; }

                var req = em.GetComponentData<AttackRequest>(e);
                if (req.Target == Entity.Null)
                { Consume(ref ecb, e); continue; }

                // If a spell is mid‑cast, do not start a new weapon action.
                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                { Consume(ref ecb, e); continue; }

                var brain = UnitBrainRegistry.Get(e);
                if (brain == null || brain.UnitCombatController == null || !brain.UnitCombatController.IsAlive)
                { Consume(ref ecb, e); continue; }

                if (!_transformLookup.HasComponent(e) || !em.HasComponent<LocalTransform>(req.Target))
                { Consume(ref ecb, e); continue; }

                // Cooldown gate
                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime)
                { Consume(ref ecb, e); continue; }

                var selfLT   = _transformLookup[e];
                var targetLT = em.GetComponentData<LocalTransform>(req.Target);
                var stats    = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                // ---- Read ranged/melee & base range from UnitStatic if present; else from UnitDefinition.weapon
                bool  isRanged;
                float baseRange;

                if (em.HasComponent<UnitStatic>(e))
                {
                    var us = em.GetComponentData<UnitStatic>(e);
                    isRanged  = (us.CombatStyle == 2);
                    baseRange = max(0.01f, us.AttackRangeBase);
                }
                else
                {
                    var w = brain.UnitDefinition?.weapon;
                    isRanged  = (w is RangedWeaponDefinition);
                    baseRange = max(0.01f, w != null ? w.attackRange : 1.5f);
                }

                float rangeMult = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;
                float effectiveRange = baseRange * max(0.0001f, rangeMult);

                if (lengthsq(selfLT.Position - targetLT.Position) > (effectiveRange * effectiveRange) * 1.1f)
                { Consume(ref ecb, e); continue; }

                var pose  = AttackPose.FromLocalTransform(selfLT);
                var uw    = em.HasComponent<UnitWeaponStatic>(e) ? em.GetComponentData<UnitWeaponStatic>(e) : default;
                bool hasUws = em.HasComponent<UnitWeaponStatic>(e);

                // Presentation (animations/feedback) sourced from UnitDefinition (unchanged)
                var weapon = brain.UnitDefinition?.weapon;

                if (!isRanged && weapon is MeleeWeaponDefinition meleeDef)
                {
                    // Build hit using statics if present, else weapon def fallback
                    var hit = BuildMeleeHitRequest(e, brain, hasUws, in uw, meleeDef, in stats, pose.Position, pose.Forward);
                    ecb.SetOrAddAndEnable(em, e, hit);

                    // Anim + feedback (presentation)
                    brain.UnitCombatController?.PlayMeleeAttackCompute();
                    if (meleeDef.attackFeedback != null)
                        FeedbackService.TryPlay(meleeDef.attackFeedback, brain.transform, (Vector3)pose.Position);

                    // Cooldown
                    float baseCd   = hasUws ? uw.MeleeAttackCooldown       : Mathf.Max(0.01f, meleeDef.attackCooldown);
                    float jitterCd = hasUws ? uw.MeleeAttackCooldownJitter : Mathf.Max(0f,    meleeDef.attackCooldownJitter);
                    var newCd = ComputeAttackCooldown(baseCd, jitterCd, stats.MeleeAttackSpeedMult, e, now);
                    ecb.SetOrAdd(em, e, newCd);
                    brain.NextAllowedAttackTime = Time.time + (newCd.NextTime - now);

                    // Movement lock
                    float lockSeconds = hasUws ? uw.MeleeSwingLockSeconds : Mathf.Max(0f, meleeDef.swingLockSeconds);
                    if (lockSeconds > 0f)
                    {
                        if (!em.HasComponent<ActionLockUntil>(e))
                            ecb.AddComponent(e, new ActionLockUntil { Until = now + lockSeconds });
                        else
                            ecb.SetComponent(e, new ActionLockUntil { Until = now + lockSeconds });
                    }
                }
                else // ranged
                {
                    bool started = StartRangedWindup(em, ref ecb, e, hasUws, in uw, weapon as RangedWeaponDefinition, in stats, pose, now);
                    if (started && weapon is RangedWeaponDefinition tempDef)
                    {
                        brain.UnitCombatController?.PlayRangedPrepareCompute();
                        if (tempDef.prepareFeedback != null)
                            FeedbackService.TryPlay(tempDef.prepareFeedback, brain.transform, pose.Position);
                    }
                }

                Consume(ref ecb, e);
            }
        }

        // RANGED helpers — use UWS if present else weapon def fallback
        private void FireRanged(EntityManager em, ref EntityCommandBuffer ecb, Entity e, UnitBrain brain,
                                in UnitWeaponStatic uw, bool hasUws, RangedWeaponDefinition rangedDef,
                                in UnitRuntimeStats stats, in AttackPose pose, float now)
        {
            float muzzleFwd = hasUws ? uw.MuzzleForward : (rangedDef?.muzzleForward ?? 0f);
            float3 muzzleOff = hasUws ? uw.MuzzleLocalOffset
                                      : new float3(rangedDef?.muzzleLocalOffset.x ?? 0f,
                                                   rangedDef?.muzzleLocalOffset.y ?? 0f,
                                                   rangedDef?.muzzleLocalOffset.z ?? 0f);

            float3 origin = pose.Position
                          + pose.Forward * max(0f, muzzleFwd)
                          + pose.Right   * muzzleOff.x
                          + pose.Up      * muzzleOff.y
                          + pose.Forward * muzzleOff.z;

            float projSpeed = hasUws ? uw.RangedProjectileSpeed       : Mathf.Max(0.01f, rangedDef?.projectileSpeed       ?? 60f);
            float maxDist   = hasUws ? uw.RangedProjectileMaxDistance : Mathf.Max(0.1f,  rangedDef?.projectileMaxDistance ?? 40f);
            float damage    = hasUws ? uw.BaseDamage                  : Mathf.Max(0f,    rangedDef?.attackDamage          ?? 1f);

            float3 aimDir = pose.Forward;
            if (em.HasComponent<Target>(e))
            {
                var targetEnt = em.GetComponentData<Target>(e).Value;
                if (targetEnt != Entity.Null && em.HasComponent<LocalTransform>(targetEnt))
                {
                    float3 targetPos = em.GetComponentData<LocalTransform>(targetEnt).Position;
                    float3 raw = targetPos - origin; raw.y = 0;
                    aimDir = math.normalizesafe(raw, pose.Forward);
                }
            }

            float baseCritChance = hasUws ? uw.RangedCritChanceBase     : Mathf.Clamp01(rangedDef?.critChance     ?? 0f);
            float baseCritMult   = hasUws ? uw.RangedCritMultiplierBase : Mathf.Max(1f,  rangedDef?.critMultiplier ?? 1f);

            float critChance   = clamp(baseCritChance + stats.CritChanceAdd, 0f, 1f);
            float critMult     = max(1f, baseCritMult * stats.CritMultiplierMult);
            float pierceChance = clamp(stats.RangedPierceChanceAdd, 0f, 1f);
            int   pierceMax    = max(0,   stats.RangedPierceMaxAdd);

            var spawn = new EcsProjectileSpawnRequest
            {
                Origin           = origin,
                Direction        = aimDir,
                Speed            = projSpeed,
                Damage           = damage,
                MaxDistance      = maxDist,
                CritChance       = critChance,
                CritMultiplier   = critMult,
                PierceChance     = pierceChance,
                PierceMaxTargets = pierceMax
            };

            ecb.SetOrAddAndEnable(em, e, spawn);

#if UNITY_EDITOR
            Debug.DrawRay((Vector3)origin, (Vector3)aimDir * 1.6f, DebugPalette.SpellFire, 0.55f, false);
#endif

            float baseCd   = hasUws ? uw.RangedAttackCooldown       : Mathf.Max(0.01f, rangedDef?.attackCooldown       ?? 0.5f);
            float jitterCd = hasUws ? uw.RangedAttackCooldownJitter : Mathf.Max(0f,    rangedDef?.attackCooldownJitter ?? 0f);
            var cd = ComputeAttackCooldown(baseCd, jitterCd, stats.RangedAttackSpeedMult, e, now);
            ecb.SetOrAdd(em, e, cd);

            brain.NextAllowedAttackTime = Time.time + (cd.NextTime - now);
        }

        private bool StartRangedWindup(EntityManager em, ref EntityCommandBuffer ecb, Entity e,
                                       bool hasUws, in UnitWeaponStatic uw, RangedWeaponDefinition rangedDef,
                                       in UnitRuntimeStats stats, in AttackPose pose, float now)
        {
            if (!em.HasComponent<AttackWindup>(e))
                ecb.AddComponent(e, new AttackWindup { Active = 0, ReleaseTime = 0 });

            var wind = em.GetComponentData<AttackWindup>(e);
            if (wind.Active != 0) return false;

            float windupSeconds = hasUws ? uw.RangedWindupSeconds : Mathf.Max(0f, rangedDef?.windupSeconds ?? 0f);
            float speedMult     = max(0.0001f, stats.RangedAttackSpeedMult);

            wind.Active      = 1;
            wind.ReleaseTime = now + windupSeconds / speedMult;
            ecb.SetComponent(e, wind);

#if UNITY_EDITOR
            Debug.DrawRay((Vector3)pose.Position + Vector3.up * 0.05f, (Vector3)pose.Forward * 1.2f, DebugPalette.SpellPrepare, 0.65f, false);
#endif
            return true;
        }

        private static MeleeHitRequest BuildMeleeHitRequest(Entity e, UnitBrain brain,
                                                            bool hasUws, in UnitWeaponStatic uw,
                                                            MeleeWeaponDefinition meleeDef,
                                                            in UnitRuntimeStats stats, float3 pos, float3 forward)
        {
            float baseDamage = hasUws ? uw.BaseDamage : Mathf.Max(1f, meleeDef.attackDamage);
            float halfAngle  = hasUws ? uw.MeleeHalfAngleDeg : Mathf.Clamp(meleeDef.halfAngleDeg, 0f, 179f);
            float invinc     = hasUws ? uw.MeleeInvincibility : Mathf.Max(0f, meleeDef.invincibility);
            int   maxTargets = hasUws ? uw.MeleeMaxTargets : Mathf.Max(1, meleeDef.maxTargets);

            float baseCritChance = hasUws ? uw.MeleeCritChanceBase     : Mathf.Clamp01(meleeDef.critChance);
            float baseCritMult   = hasUws ? uw.MeleeCritMultiplierBase : Mathf.Max(1f, meleeDef.critMultiplier);

            float critChance     = clamp(baseCritChance + stats.CritChanceAdd, 0f, 1f);
            float critMultiplier = max(1f, baseCritMult * stats.MeleeRangeMult); // keep original semantics

            // Range = UnitDefinition.attackRange * stats (original behavior).
            float baseRange = Mathf.Max(0.01f, brain.UnitDefinition?.weapon != null ? brain.UnitDefinition.weapon.attackRange : 1.5f);
            float range     = baseRange * max(0.0001f, stats.MeleeRangeMult);

            return new MeleeHitRequest
            {
                Origin        = pos,
                Forward       = forward,
                Range         = range,
                HalfAngleRad  = radians(clamp(halfAngle * max(0.0001f, stats.MeleeArcMult), 0f, 179f)),
                Damage        = max(1f, baseDamage),
                Invincibility = max(0f, invinc),
                LayerMask     = (UnitBrainRegistry.Get(e)?.GetDamageableLayerMask().value) ?? ~0,
                MaxTargets    = max(1, maxTargets),
                CritChance    = critChance,
                CritMultiplier= critMultiplier,
            };
        }

        private static void Consume(ref EntityCommandBuffer ecb, Entity e)
        {
            ecb.SetComponent(e, new AttackRequest { Target = Entity.Null });
            ecb.SetComponentEnabled<AttackRequest>(e, false);
        }

        private static AttackCooldown ComputeAttackCooldown(float baseCd, float jitterRange, float speedMult, Entity e, float now)
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
