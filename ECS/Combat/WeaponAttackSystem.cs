using OneBitRob.ECS;
using OneBitRob.FX;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AttackTargetSystem))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial struct WeaponAttackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _posRO;
        private EntityQuery _attackQuery;
        private EntityQuery _windupQuery;

        public void OnCreate(ref SystemState state)
        {
            _posRO = state.GetComponentLookup<LocalTransform>(true);
            _attackQuery = state.GetEntityQuery(ComponentType.ReadWrite<AttackRequest>());
            _windupQuery = state.GetEntityQuery(ComponentType.ReadWrite<AttackWindup>());

            state.RequireForUpdate(state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<AttackRequest>(), ComponentType.ReadOnly<AttackWindup>() }
            }));
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            float now = (float)SystemAPI.Time.ElapsedTime;

            // 1) Release finished windups (ranged fire moment)
            var windupEntities = _windupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < windupEntities.Length; i++)
            {
                var e = windupEntities[i];
                if (!em.HasComponent<AttackWindup>(e)) continue;
                var w = em.GetComponentData<AttackWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                    continue; // postpone while spells cast

                var brain  = UnitBrainRegistry.Get(e);
                var weaponDef = brain?.UnitDefinition?.weapon;

                if (brain?.CombatSubsystem == null || !brain.CombatSubsystem.IsAlive || !_posRO.HasComponent(e))
                {
                    w.Active = 0;
                    ecb.SetComponent(e, w);
                    continue;
                }

                float3 selfPos = _posRO[e].Position;
                var rot = _posRO[e].Rotation;
                float3 fwd   = normalizesafe(mul(rot, new float3(0, 0, 1)));
                float3 up    = normalizesafe(mul(rot, new float3(0, 1, 0)));
                float3 right = normalizesafe(mul(rot, new float3(1, 0, 0)));

                var stats = em.HasComponent<UnitRuntimeStats>(e)
                    ? em.GetComponentData<UnitRuntimeStats>(e)
                    : UnitRuntimeStats.Defaults;

                if (weaponDef is RangedWeaponDefinition rw)
                {
                    float3 origin = selfPos
                                  + fwd   * max(0f, rw.muzzleForward)
                                  + right * rw.muzzleLocalOffset.x
                                  + up    * rw.muzzleLocalOffset.y
                                  + fwd   * rw.muzzleLocalOffset.z;

                    float3 aimDir = fwd;
                    if (em.HasComponent<Target>(e))
                    {
                        var tgt = em.GetComponentData<Target>(e).Value;
                        if (tgt != Entity.Null && _posRO.HasComponent(tgt))
                        {
                            float3 tgtPos = _posRO[tgt].Position;
                            float3 raw = tgtPos - origin; raw.y = 0;
                            aimDir = normalizesafe(raw, fwd);
                        }
                    }

                    // Crit/pierce final
                    float critChance = clamp(rw.critChance + stats.CritChanceAdd, 0f, 1f);
                    float critMult   = max(1f, rw.critMultiplier * stats.CritMultiplierMult);
                    float pierceChance = clamp(stats.RangedPierceChanceAdd, 0f, 1f);
                    int   pierceMax    = max(0, stats.RangedPierceMaxAdd);

                    var spawn = new EcsProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = aimDir,
                        Speed       = max(0.01f, rw.projectileSpeed),
                        Damage      = max(0f, rw.attackDamage),
                        MaxDistance = max(0.1f, rw.projectileMaxDistance),
                        CritChance  = critChance,
                        CritMultiplier = critMult,
                        PierceChance = pierceChance,
                        PierceMaxTargets = pierceMax,
                        HasValue    = 1
                    };

                    if (em.HasComponent<EcsProjectileSpawnRequest>(e)) ecb.SetComponent(e, spawn);
                    else                                                             ecb.AddComponent(e, spawn);

                    brain.CombatSubsystem?.PlayRangedFire(rw.animations);
                    FeedbackService.TryPlay(rw.fireFeedback, brain.transform, (UnityEngine.Vector3)origin);

                    float jitter = CalcJitter(rw.attackCooldownJitter, e, now);
                    // cooldown scaled by ranged AS multiplier
                    float speedMult = max(0.0001f, stats.RangedAttackSpeedMult);
                    var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                    cd.NextTime = now + max(0.01f, rw.attackCooldown) / speedMult + jitter;
                    if (em.HasComponent<AttackCooldown>(e)) ecb.SetComponent(e, cd);
                    else                                    ecb.AddComponent(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + rw.attackCooldown / speedMult + jitter;
                }
                else if (weaponDef is MeleeWeaponDefinition mw)
                {
                    // Melee path here is rare (windup-melee), ale ustawmy crit i skalowania poprawnie
                    var statsLocal = stats; // alias
                    var hit = new MeleeHitRequest
                    {
                        Origin        = selfPos,
                        Forward       = fwd,
                        Range         = max(0.01f, mw.attackRange * max(0.0001f, statsLocal.MeleeRangeMult)),
                        HalfAngleRad  = radians(clamp(mw.halfAngleDeg * max(0.0001f, statsLocal.MeleeArcMult), 0f, 179f)),
                        Damage        = max(1f, mw.attackDamage),
                        Invincibility = max(0f, mw.invincibility),
                        LayerMask     = (UnitBrainRegistry.Get(e)?.GetDamageableLayerMask().value) ?? ~0,
                        MaxTargets    = max(1, mw.maxTargets),
                        CritChance    = clamp(mw.critChance + statsLocal.CritChanceAdd, 0f, 1f),
                        CritMultiplier= max(1f, mw.critMultiplier * statsLocal.CritMultiplierMult),
                        HasValue      = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(e)) ecb.SetComponent(e, hit);
                    else                                                   ecb.AddComponent(e, hit);

                    brain.CombatSubsystem?.PlayMeleeAttack(mw.attackAnimations);
                    FeedbackService.TryPlay(mw.attackFeedback, brain.transform, (UnityEngine.Vector3)selfPos);

                    float jitter = CalcJitter(mw.attackCooldownJitter, e, now);
                    float speedMult = max(0.0001f, stats.MeleeAttackSpeedMult);
                    var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                    cd.NextTime = now + max(0.01f, mw.attackCooldown) / speedMult + jitter;
                    if (em.HasComponent<AttackCooldown>(e)) ecb.SetComponent(e, cd);
                    else                                    ecb.AddComponent(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + mw.attackCooldown / speedMult + jitter;
                }

                w.Active = 0;
                ecb.SetComponent(e, w);
            }
            windupEntities.Dispose();

            // 2) Consume new AttackRequests
            var entities = _attackQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e   = entities[i];
                var req = em.GetComponentData<AttackRequest>(e);
                if (req.HasValue == 0 || req.Target == Entity.Null) { Consume(ref ecb, e); continue; }

                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                { Consume(ref ecb, e); continue; }

                var brain  = UnitBrainRegistry.Get(e);
                var weapon = brain?.UnitDefinition?.weapon;

                var stats = em.HasComponent<UnitRuntimeStats>(e)
                    ? em.GetComponentData<UnitRuntimeStats>(e)
                    : UnitRuntimeStats.Defaults;

                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime) { Consume(ref ecb, e); continue; }

                if (!_posRO.HasComponent(e) || !_posRO.HasComponent(req.Target))
                { Consume(ref ecb, e); continue; }

                float3 selfPos = _posRO[e].Position;
                float3 tgtPos  = _posRO[req.Target].Position;

                bool isRanged = weapon is RangedWeaponDefinition;
                float baseRange = max(0.01f, weapon != null ? weapon.attackRange : 1.5f);
                float rangeMult = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;
                float effectiveRange = baseRange * max(0.0001f, rangeMult);

                if (lengthsq(selfPos - tgtPos) > (effectiveRange * effectiveRange) * 1.1f)
                { Consume(ref ecb, e); continue; }

                float3 fwd = normalizesafe(mul(_posRO[e].Rotation, new float3(0, 0, 1)));

                if (weapon is MeleeWeaponDefinition mw2)
                {
                    var hit = new MeleeHitRequest
                    {
                        Origin        = selfPos,
                        Forward       = fwd,
                        Range         = max(0.01f, mw2.attackRange * max(0.0001f, stats.MeleeRangeMult)),
                        HalfAngleRad  = radians(clamp(mw2.halfAngleDeg * max(0.0001f, stats.MeleeArcMult), 0f, 179f)),
                        Damage        = max(1f, mw2.attackDamage),
                        Invincibility = max(0f, mw2.invincibility),
                        LayerMask     = brain.GetDamageableLayerMask().value,
                        MaxTargets    = max(1, mw2.maxTargets),
                        CritChance    = clamp(mw2.critChance + stats.CritChanceAdd, 0f, 1f),
                        CritMultiplier= max(1f, mw2.critMultiplier * stats.CritMultiplierMult),
                        HasValue      = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(e)) ecb.SetComponent(e, hit);
                    else                                                   ecb.AddComponent(e, hit);

                    brain.CombatSubsystem?.PlayMeleeAttack(mw2.attackAnimations);
                    FeedbackService.TryPlay(mw2.attackFeedback, brain.transform, (UnityEngine.Vector3)selfPos);

                    float hold = max(0f, mw2.lockWhileFiringSeconds);
                    if (hold > 0f)
                    {
                        var win = new ActionLockUntil { Until = now + hold };
                        if (em.HasComponent<ActionLockUntil>(e)) ecb.SetComponent(e, win);
                        else                                                    ecb.AddComponent(e, win);
                    }

                    float jitter = CalcJitter(mw2.attackCooldownJitter, e, now);
                    float speedMult = max(0.0001f, stats.MeleeAttackSpeedMult);
                    cd.NextTime = now + max(0.01f, mw2.attackCooldown) / speedMult + jitter;
                    if (em.HasComponent<AttackCooldown>(e)) ecb.SetComponent(e, cd);
                    else                                    ecb.AddComponent(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + mw2.attackCooldown / speedMult + jitter;
                }
                else if (weapon is RangedWeaponDefinition rw2)
                {
                    if (!em.HasComponent<AttackWindup>(e))
                        ecb.AddComponent(e, new AttackWindup { Active = 0, ReleaseTime = 0 });

                    var w = em.HasComponent<AttackWindup>(e) ? em.GetComponentData<AttackWindup>(e) : default;
                    if (w.Active == 0)
                    {
                        float speedMult = max(0.0001f, stats.RangedAttackSpeedMult);
                        w.Active      = 1;
                        w.ReleaseTime = now + max(0f, rw2.windupSeconds) / speedMult;
                        ecb.SetComponent(e, w);
                        brain.CombatSubsystem?.PlayRangedPrepare(rw2.animations);

                        FeedbackService.TryPlay(rw2.prepareFeedback, brain.transform, (UnityEngine.Vector3)selfPos);
                    }
                }

                Consume(ref ecb, e);
            }
            entities.Dispose();

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
