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
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AttackTargetSystem))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial struct WeaponAttackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup; // RO
        private EntityQuery _attackRequestQuery;
        private EntityQuery _windupQuery;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup   = state.GetComponentLookup<LocalTransform>(true);
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

            // 1) Release finished windups (ranged fire moment)
            var windupEntities = _windupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < windupEntities.Length; i++)
            {
                var entity = windupEntities[i];
                if (!em.HasComponent<AttackWindup>(entity)) continue;
                var windup = em.GetComponentData<AttackWindup>(entity);
                if (windup.Active == 0 || now < windup.ReleaseTime) continue;

                if (em.HasComponent<SpellWindup>(entity) && em.GetComponentData<SpellWindup>(entity).Active != 0)
                    continue; // postpone while spells cast

                var brain  = UnitBrainRegistry.Get(entity);
                var weaponDef = brain?.UnitDefinition?.weapon;

                if (brain?.UnitCombatController == null || !brain.UnitCombatController.IsAlive || !_transformLookup.HasComponent(entity))
                {
                    windup.Active = 0;
                    ecb.SetComponent(entity, windup);
                    continue;
                }

                float3 selfPos = _transformLookup[entity].Position;
                var rot = _transformLookup[entity].Rotation;
                float3 forward = normalizesafe(mul(rot, new float3(0, 0, 1)));
                float3 up      = normalizesafe(mul(rot, new float3(0, 1, 0)));
                float3 right   = normalizesafe(mul(rot, new float3(1, 0, 0)));

                var stats = em.HasComponent<UnitRuntimeStats>(entity)
                    ? em.GetComponentData<UnitRuntimeStats>(entity)
                    : UnitRuntimeStats.Defaults;

                if (weaponDef is RangedWeaponDefinition ranged)
                {
                    float3 origin = selfPos
                                  + forward * max(0f, ranged.muzzleForward)
                                  + right   * ranged.muzzleLocalOffset.x
                                  + up      * ranged.muzzleLocalOffset.y
                                  + forward * ranged.muzzleLocalOffset.z;

                    float3 aimDir = forward;
                    if (em.HasComponent<Target>(entity))
                    {
                        var targetEnt = em.GetComponentData<Target>(entity).Value;
                        if (targetEnt != Entity.Null && _transformLookup.HasComponent(targetEnt))
                        {
                            float3 targetPos = _transformLookup[targetEnt].Position;
                            float3 raw = targetPos - origin; raw.y = 0;
                            aimDir = normalizesafe(raw, forward);
                        }
                    }

                    // Crit/pierce final
                    float critChance = clamp(ranged.critChance + stats.CritChanceAdd, 0f, 1f);
                    float critMult   = max(1f, ranged.critMultiplier * stats.CritMultiplierMult);
                    float pierceChance = clamp(stats.RangedPierceChanceAdd, 0f, 1f);
                    int   pierceMax    = max(0, stats.RangedPierceMaxAdd);

                    var spawn = new EcsProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = aimDir,
                        Speed       = max(0.01f, ranged.projectileSpeed),
                        Damage      = max(0f, ranged.attackDamage),
                        MaxDistance = max(0.1f, ranged.projectileMaxDistance),
                        CritChance  = critChance,
                        CritMultiplier = critMult,
                        PierceChance = pierceChance,
                        PierceMaxTargets = pierceMax,
                        HasValue    = 1
                    };

                    if (em.HasComponent<EcsProjectileSpawnRequest>(entity)) ecb.SetComponent(entity, spawn);
                    else                                                   ecb.AddComponent(entity, spawn);

                    brain.UnitCombatController?.PlayRangedFire(ranged.animations);
                    FeedbackService.TryPlay(ranged.fireFeedback, brain.transform, (Vector3)origin);

#if UNITY_EDITOR
                    Debug.DrawRay((Vector3)origin, (Vector3)aimDir * 1.6f, new Color(1f, 0.45f, 0.2f, 0.95f), 0.55f, false);
#endif

                    float jitter = CalcJitter(ranged.attackCooldownJitter, entity, now);
                    float speedMult = max(0.0001f, stats.RangedAttackSpeedMult);
                    var cd = em.HasComponent<AttackCooldown>(entity) ? em.GetComponentData<AttackCooldown>(entity) : default;
                    cd.NextTime = now + max(0.01f, ranged.attackCooldown) / speedMult + jitter;
                    if (em.HasComponent<AttackCooldown>(entity)) ecb.SetComponent(entity, cd);
                    else                                         ecb.AddComponent(entity, cd);

                    brain.NextAllowedAttackTime = Time.time + ranged.attackCooldown / speedMult + jitter;
                }
                else if (weaponDef is MeleeWeaponDefinition melee)
                {
                    var statsLocal = stats; // alias
                    var hit = new MeleeHitRequest
                    {
                        Origin        = selfPos,
                        Forward       = forward,
                        Range         = max(0.01f, melee.attackRange * max(0.0001f, statsLocal.MeleeRangeMult)),
                        HalfAngleRad  = radians(clamp(melee.halfAngleDeg * max(0.0001f, statsLocal.MeleeArcMult), 0f, 179f)),
                        Damage        = max(1f, melee.attackDamage),
                        Invincibility = max(0f, melee.invincibility),
                        LayerMask     = (UnitBrainRegistry.Get(entity)?.GetDamageableLayerMask().value) ?? ~0,
                        MaxTargets    = max(1, melee.maxTargets),
                        CritChance    = clamp(melee.critChance + statsLocal.CritChanceAdd, 0f, 1f),
                        CritMultiplier= max(1f, melee.critMultiplier * statsLocal.CritMultiplierMult),
                        HasValue      = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(entity)) ecb.SetComponent(entity, hit);
                    else                                         ecb.AddComponent(entity, hit);

                    brain.UnitCombatController?.PlayMeleeAttack(melee.attackAnimations);
                    FeedbackService.TryPlay(melee.attackFeedback, brain.transform, (Vector3)selfPos);

                    float jitter = CalcJitter(melee.attackCooldownJitter, entity, now);
                    float speedMult = max(0.0001f, stats.MeleeAttackSpeedMult);
                    var cd = em.HasComponent<AttackCooldown>(entity) ? em.GetComponentData<AttackCooldown>(entity) : default;
                    cd.NextTime = now + max(0.01f, melee.attackCooldown) / speedMult + jitter;
                    if (em.HasComponent<AttackCooldown>(entity)) ecb.SetComponent(entity, cd);
                    else                                         ecb.AddComponent(entity, cd);

                    brain.NextAllowedAttackTime = Time.time + melee.attackCooldown / speedMult + jitter;
                }

                windup.Active = 0;
                ecb.SetComponent(entity, windup);
            }
            windupEntities.Dispose();

            // 2) Consume new AttackRequests
            var requestEntities = _attackRequestQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < requestEntities.Length; i++)
            {
                var entity   = requestEntities[i];
                var request = em.GetComponentData<AttackRequest>(entity);
                if (request.HasValue == 0 || request.Target == Entity.Null) { Consume(ref ecb, entity); continue; }

                if (em.HasComponent<SpellWindup>(entity) && em.GetComponentData<SpellWindup>(entity).Active != 0)
                { Consume(ref ecb, entity); continue; }

                var brain  = UnitBrainRegistry.Get(entity);
                var weapon = brain?.UnitDefinition?.weapon;

                var stats = em.HasComponent<UnitRuntimeStats>(entity)
                    ? em.GetComponentData<UnitRuntimeStats>(entity)
                    : UnitRuntimeStats.Defaults;

                var cd = em.HasComponent<AttackCooldown>(entity) ? em.GetComponentData<AttackCooldown>(entity) : default;
                if (now < cd.NextTime) { Consume(ref ecb, entity); continue; }

                if (!_transformLookup.HasComponent(entity) || !_transformLookup.HasComponent(request.Target))
                { Consume(ref ecb, entity); continue; }

                float3 selfPos = _transformLookup[entity].Position;
                float3 targetPos  = _transformLookup[request.Target].Position;

                bool isRanged = weapon is RangedWeaponDefinition;
                float baseRange = max(0.01f, weapon != null ? weapon.attackRange : 1.5f);
                float rangeMult = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;
                float effectiveRange = baseRange * max(0.0001f, rangeMult);

                if (lengthsq(selfPos - targetPos) > (effectiveRange * effectiveRange) * 1.1f)
                { Consume(ref ecb, entity); continue; }

                float3 forward = normalizesafe(mul(_transformLookup[entity].Rotation, new float3(0, 0, 1)));

                if (weapon is MeleeWeaponDefinition melee2)
                {
                    var hit = new MeleeHitRequest
                    {
                        Origin        = selfPos,
                        Forward       = forward,
                        Range         = max(0.01f, melee2.attackRange * max(0.0001f, stats.MeleeRangeMult)),
                        HalfAngleRad  = radians(clamp(melee2.halfAngleDeg * max(0.0001f, stats.MeleeArcMult), 0f, 179f)),
                        Damage        = max(1f, melee2.attackDamage),
                        Invincibility = max(0f, melee2.invincibility),
                        LayerMask     = brain.GetDamageableLayerMask().value,
                        MaxTargets    = max(1, melee2.maxTargets),
                        CritChance    = clamp(melee2.critChance + stats.CritChanceAdd, 0f, 1f),
                        CritMultiplier= max(1f, melee2.critMultiplier * stats.CritMultiplierMult),
                        HasValue      = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(entity)) ecb.SetComponent(entity, hit);
                    else                                         ecb.AddComponent(entity, hit);

                    brain.UnitCombatController?.PlayMeleeAttack(melee2.attackAnimations);
                    FeedbackService.TryPlay(melee2.attackFeedback, brain.transform, (Vector3)selfPos);
                }
                else if (weapon is RangedWeaponDefinition ranged2)
                {
                    if (!em.HasComponent<AttackWindup>(entity))
                        ecb.AddComponent(entity, new AttackWindup { Active = 0, ReleaseTime = 0 });

                    var windup = em.HasComponent<AttackWindup>(entity) ? em.GetComponentData<AttackWindup>(entity) : default;
                    if (windup.Active == 0)
                    {
                        float speedMult = max(0.0001f, stats.RangedAttackSpeedMult);
                        windup.Active      = 1;
                        windup.ReleaseTime = now + max(0f, ranged2.windupSeconds) / speedMult;
                        ecb.SetComponent(entity, windup);
                        brain.UnitCombatController?.PlayRangedPrepare(ranged2.animations);

                        FeedbackService.TryPlay(ranged2.prepareFeedback, brain.transform, (Vector3)selfPos);

#if UNITY_EDITOR
                        Debug.DrawRay((Vector3)selfPos + Vector3.up * 0.05f, (Vector3)forward * 1.2f, new Color(0.8f, 0.9f, 1f, 0.95f), 0.65f, false);
#endif
                    }
                }

                Consume(ref ecb, entity);
            }
            requestEntities.Dispose();

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void Consume(ref EntityCommandBuffer ecb, Entity e)
            => ecb.SetComponent(e, new AttackRequest { HasValue = 0, Target = Entity.Null });

        private static float CalcJitter(float range, Entity e, float now)
        {
            if (range <= 0f) return 0f;
            uint h = math.hash(new float3(now, e.Index, e.Version));
            float u = (h / (float)uint.MaxValue) * 2f - 1f;
            return u * range;
        }
    }
}
