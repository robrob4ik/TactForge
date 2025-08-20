using OneBitRob.ECS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AttackTargetSystem))]
    [UpdateAfter(typeof(SpellExecutionSystem))] // ← ensure spells finish before weapon attacks this frame
    public partial struct AttackExecutionSystem : ISystem
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
            var now = (float)SystemAPI.Time.ElapsedTime;
            var em  = state.EntityManager;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1) Release windups (ranged fire moment) — DEFER IF SPELL CASTING
            var windupEntities = _windupQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < windupEntities.Length; i++)
            {
                var e = windupEntities[i];
                if (!em.HasComponent<AttackWindup>(e)) continue;
                var w = em.GetComponentData<AttackWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                // NEW: if spell cast is in progress, postpone release
                if (em.HasComponent<SpellWindup>(e))
                {
                    var sw = em.GetComponentData<SpellWindup>(e);
                    if (sw.Active != 0)
                        continue; // keep weapon windup waiting until spell finishes
                }

                var brain  = UnitBrainRegistry.Get(e);
                var weapon = brain?.UnitDefinition?.weapon;

                if (brain?.CombatSubsystem == null || !brain.CombatSubsystem.IsAlive || !_posRO.HasComponent(e))
                {
                    w.Active = 0;
                    ecb.SetComponent(e, w);
                    continue;
                }

                float3 selfPos = _posRO[e].Position;
                var rot = _posRO[e].Rotation;
                float3 fwd   = math.normalizesafe(math.mul(rot, new float3(0, 0, 1)));
                float3 up    = math.normalizesafe(math.mul(rot, new float3(0, 1, 0)));
                float3 right = math.normalizesafe(math.mul(rot, new float3(1, 0, 0)));

                if (weapon is RangedWeaponDefinition rw)
                {
                    // Compute muzzle origin first
                    float3 origin = selfPos
                                  + fwd   * math.max(0f, rw.muzzleForward)
                                  + right * rw.muzzleLocalOffset.x
                                  + up    * rw.muzzleLocalOffset.y
                                  + fwd   * rw.muzzleLocalOffset.z;

                    // Compute aim direction to current target (fallback to forward)
                    float3 aimDir = fwd;
                    if (em.HasComponent<Target>(e))
                    {
                        var tgt = em.GetComponentData<Target>(e).Value;
                        if (tgt != Entity.Null && _posRO.HasComponent(tgt))
                        {
                            float3 tgtPos = _posRO[tgt].Position;
                            float3 raw = tgtPos - origin;
                            raw.y = 0; // keep topdown planar aiming
                            aimDir = math.normalizesafe(raw, fwd);
                        }
                    }

#if UNITY_EDITOR
                    Debug.DrawRay((Vector3)origin, (Vector3)(aimDir * 1.25f), Color.red, 0.15f, false);
#endif

                    var spawn = new EcsProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = aimDir,
                        Speed       = math.max(0.01f, rw.projectileSpeed),
                        Damage      = math.max(0f, rw.attackDamage),
                        MaxDistance = math.max(0.1f, rw.projectileMaxDistance),
                        HasValue    = 1
                    };

                    if (em.HasComponent<EcsProjectileSpawnRequest>(e)) ecb.SetComponent(e, spawn);
                    else                                             ecb.AddComponent(e, spawn);

                    brain.CombatSubsystem?.PlayRangedFire(rw.animations);

                    var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                    float jitter = CalcJitter(rw.attackCooldownJitter, e, now);
                    cd.NextTime = now + math.max(0.01f, rw.attackCooldown) + jitter;

                    if (em.HasComponent<AttackCooldown>(e)) ecb.SetComponent(e, cd);
                    else                                   ecb.AddComponent(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + rw.attackCooldown + jitter;
                }
                else if (weapon is MeleeWeaponDefinition mw)
                {
                    var hit = new MeleeHitRequest
                    {
                        Origin        = selfPos,
                        Forward       = fwd,
                        Range         = math.max(0.01f, mw.attackRange),
                        HalfAngleRad  = math.radians(math.clamp(mw.halfAngleDeg, 0f, 179f)),
                        Damage        = math.max(1f, mw.attackDamage),
                        Invincibility = math.max(0f, mw.invincibility),
                        LayerMask     = (UnitBrainRegistry.Get(e)?.GetDamageableLayerMask().value) ?? ~0,
                        MaxTargets    = math.max(1, mw.maxTargets),
                        HasValue      = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(e)) ecb.SetComponent(e, hit);
                    else                                    ecb.AddComponent(e, hit);

                    brain.CombatSubsystem?.PlayMeleeAttack(mw.attackAnimations);

                    var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                    float jitter = CalcJitter(mw.attackCooldownJitter, e, now);
                    cd.NextTime = now + math.max(0.01f, mw.attackCooldown) + jitter;

                    if (em.HasComponent<AttackCooldown>(e)) ecb.SetComponent(e, cd);
                    else                                   ecb.AddComponent(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + mw.attackCooldown + jitter;
                }

                w.Active = 0;
                ecb.SetComponent(e, w);
            }
            windupEntities.Dispose();

            // 2) Process incoming attack requests — BLOCK WHILE CASTING
            var entities = _attackQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e   = entities[i];
                var req = em.GetComponentData<AttackRequest>(e);
                if (req.HasValue == 0 || req.Target == Entity.Null)
                {
                    Consume(ref ecb, e);
                    continue;
                }

                // NEW: if a spell is in progress, do not start weapon attacks (and don't play prepare anims)
                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                {
                    Consume(ref ecb, e);
                    continue;
                }

                var brain  = UnitBrainRegistry.Get(e);
                var weapon = brain?.UnitDefinition?.weapon;

                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime) { Consume(ref ecb, e); continue; }

                if (!_posRO.HasComponent(e) || !_posRO.HasComponent(req.Target))
                { Consume(ref ecb, e); continue; }

                float3 selfPos = _posRO[e].Position;
                float3 tgtPos  = _posRO[req.Target].Position;

                float range   = math.max(0.01f, weapon != null ? weapon.attackRange : 1.5f);
                float rangeSq = range * range;
                if (math.lengthsq(selfPos - tgtPos) > rangeSq * 1.1f)
                { Consume(ref ecb, e); continue; }

                float3 fwd = math.normalizesafe(math.mul(_posRO[e].Rotation, new float3(0, 0, 1)));

                if (weapon is MeleeWeaponDefinition mw2)
                {
                    var hit = new MeleeHitRequest
                    {
                        Origin        = selfPos,
                        Forward       = fwd,
                        Range         = math.max(0.01f, mw2.attackRange),
                        HalfAngleRad  = math.radians(math.clamp(mw2.halfAngleDeg, 0f, 179f)),
                        Damage        = math.max(1f, mw2.attackDamage),
                        Invincibility = math.max(0f, mw2.invincibility),
                        LayerMask     = brain.GetDamageableLayerMask().value,
                        MaxTargets    = math.max(1, mw2.maxTargets),
                        HasValue      = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(e)) ecb.SetComponent(e, hit);
                    else                                    ecb.AddComponent(e, hit);

                    brain.CombatSubsystem?.PlayMeleeAttack(mw2.attackAnimations);

                    float jitter = CalcJitter(mw2.attackCooldownJitter, e, now);
                    cd.NextTime = now + math.max(0.01f, mw2.attackCooldown) + jitter;
                    if (em.HasComponent<AttackCooldown>(e)) ecb.SetComponent(e, cd);
                    else                                   ecb.AddComponent(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + mw2.attackCooldown + jitter;
                }
                else if (weapon is RangedWeaponDefinition rw2)
                {
                    if (!em.HasComponent<AttackWindup>(e))
                        ecb.AddComponent(e, new AttackWindup { Active = 0, ReleaseTime = 0 });

                    var w = em.HasComponent<AttackWindup>(e) ? em.GetComponentData<AttackWindup>(e) : default;
                    if (w.Active == 0)
                    {
                        w.Active      = 1;
                        w.ReleaseTime = now + math.max(0f, rw2.windupSeconds);
                        ecb.SetComponent(e, w);

                        brain.CombatSubsystem?.PlayRangedPrepare(rw2.animations);
                    }
                }

                Consume(ref ecb, e);
            }
            entities.Dispose();

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void Consume(ref EntityCommandBuffer ecb, Entity e)
        {
            ecb.SetComponent(e, new AttackRequest { HasValue = 0, Target = Entity.Null });
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
