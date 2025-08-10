using OneBitRob.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AttackTargetSystem))]
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

            state.RequireForUpdate(
                state.GetEntityQuery(
                    new EntityQueryDesc
                    {
                        Any = new[] { ComponentType.ReadOnly<AttackRequest>(), ComponentType.ReadOnly<AttackWindup>() }
                    }
                )
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            var now = (float)SystemAPI.Time.ElapsedTime;
            var em = state.EntityManager;

            // 1) Release windups (ranged fire moment)
            var windupEntities = _windupQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < windupEntities.Length; i++)
            {
                var e = windupEntities[i];
                if (!em.HasComponent<AttackWindup>(e)) continue;
                var w = em.GetComponentData<AttackWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain == null)
                {
                    w.Active = 0;
                    em.SetComponentData(e, w);
                    continue;
                }

                var weapon = brain.UnitDefinition ? brain.UnitDefinition.weapon : null;

                if (brain.CombatSubsystem == null || !brain.CombatSubsystem.IsAlive)
                {
                    w.Active = 0;
                    em.SetComponentData(e, w);
                    continue;
                }

                if (!_posRO.HasComponent(e))
                {
                    w.Active = 0;
                    em.SetComponentData(e, w);
                    continue;
                }

                float3 selfPos = _posRO[e].Position;
                float3 forward = math.normalizesafe(math.mul(_posRO[e].Rotation, new float3(0, 0, 1)));

                if (weapon is RangedWeaponDefinition rw)
                {
                    var spawn = new EcsProjectileSpawnRequest
                    {
                        Origin = selfPos + forward * math.max(0f, rw.muzzleForward),
                        Direction = forward,
                        Speed = math.max(0.01f, rw.projectileSpeed),
                        Damage = math.max(0f, rw.attackDamage),
                        MaxDistance = math.max(0.1f, rw.projectileMaxDistance),
                        HasValue = 1
                    };
                    if (em.HasComponent<EcsProjectileSpawnRequest>(e))
                        em.SetComponentData(e, spawn);
                    else
                        em.AddComponentData(e, spawn);

                    brain.CombatSubsystem?.PlayRangedFire(rw.animations);

                    var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                    cd.NextTime = now + math.max(0.01f, rw.attackCooldown);
                    if (em.HasComponent<AttackCooldown>(e))
                        em.SetComponentData(e, cd);
                    else
                        em.AddComponentData(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + rw.attackCooldown;
                }
                else if (weapon is MeleeWeaponDefinition mw)
                {
                    var hit = new MeleeHitRequest
                    {
                        Origin = selfPos,
                        Forward = forward,
                        Range = math.max(0.01f, mw.attackRange),
                        HalfAngleRad = math.radians(math.clamp(mw.halfAngleDeg, 0f, 179f)),
                        Damage = math.max(1f, mw.attackDamage),
                        Invincibility = math.max(0f, mw.invincibility),
                        LayerMask = (UnitBrainRegistry.Get(e)?.GetTargetLayerMask().value) ?? ~0,
                        MaxTargets = math.max(1, mw.maxTargets),
                        HasValue = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(e))
                        em.SetComponentData(e, hit);
                    else
                        em.AddComponentData(e, hit);

                    brain.CombatSubsystem?.PlayMeleeAttack(mw.attackAnimations);

                    var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                    cd.NextTime = now + math.max(0.01f, mw.attackCooldown);
                    if (em.HasComponent<AttackCooldown>(e))
                        em.SetComponentData(e, cd);
                    else
                        em.AddComponentData(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + mw.attackCooldown;
                }

                w.Active = 0;
                em.SetComponentData(e, w);
            }

            windupEntities.Dispose();

            // 2) Process incoming attack requests
            var entities = _attackQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var req = em.GetComponentData<AttackRequest>(e);
                if (req.HasValue == 0 || req.Target == Entity.Null) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain == null)
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                var weapon = brain.UnitDefinition ? brain.UnitDefinition.weapon : null;

                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime)
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                if (!_posRO.HasComponent(e) || !_posRO.HasComponent(req.Target))
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                float3 selfPos = _posRO[e].Position;
                float3 tgtPos = _posRO[req.Target].Position;

                float range = math.max(0.01f, weapon != null ? weapon.attackRange : 1.5f);
                float rangeSq = range * range;
                if (math.lengthsq(selfPos - tgtPos) > rangeSq * 1.1f)
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                float3 forward = math.normalizesafe(math.mul(_posRO[e].Rotation, new float3(0, 0, 1)));

                if (weapon is MeleeWeaponDefinition mw)
                {
                    var hit = new MeleeHitRequest
                    {
                        Origin = selfPos,
                        Forward = forward,
                        Range = math.max(0.01f, mw.attackRange),
                        HalfAngleRad = math.radians(math.clamp(mw.halfAngleDeg, 0f, 179f)),
                        Damage = math.max(1f, mw.attackDamage),
                        Invincibility = math.max(0f, mw.invincibility),
                        LayerMask = brain.GetTargetLayerMask().value,
                        MaxTargets = math.max(1, mw.maxTargets),
                        HasValue = 1
                    };
                    if (em.HasComponent<MeleeHitRequest>(e))
                        em.SetComponentData(e, hit);
                    else
                        em.AddComponentData(e, hit);

                    brain.CombatSubsystem?.PlayMeleeAttack(mw.attackAnimations);

                    cd.NextTime = now + math.max(0.01f, mw.attackCooldown);
                    if (em.HasComponent<AttackCooldown>(e))
                        em.SetComponentData(e, cd);
                    else
                        em.AddComponentData(e, cd);

                    brain.NextAllowedAttackTime = UnityEngine.Time.time + mw.attackCooldown;
                }
                else if (weapon is RangedWeaponDefinition rw)
                {
                    if (!em.HasComponent<AttackWindup>(e)) em.AddComponentData(e, new AttackWindup { Active = 0, ReleaseTime = 0 });

                    var w = em.GetComponentData<AttackWindup>(e);
                    if (w.Active == 0) // don't stack
                    {
                        w.Active = 1;
                        w.ReleaseTime = now + math.max(0f, rw.windupSeconds);
                        em.SetComponentData(e, w);

                        brain.CombatSubsystem?.PlayRangedPrepare(rw.animations);
                    }
                }

                Consume(ref em, ref req, e);
            }

            entities.Dispose();
        }

        private static void Consume(ref EntityManager em, ref AttackRequest req, Entity e)
        {
            req.HasValue = 0;
            req.Target = Entity.Null;
            em.SetComponentData(e, req);
        }
    }
}