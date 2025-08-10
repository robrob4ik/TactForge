using OneBitRob.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AttackTargetSystem))]
    public partial struct AttackExecutionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _posRO;
        private EntityQuery _attackQuery;

        public void OnCreate(ref SystemState state)
        {
            _posRO = state.GetComponentLookup<LocalTransform>(true);
            _attackQuery = state.GetEntityQuery(ComponentType.ReadWrite<AttackRequest>());
            state.RequireForUpdate(_attackQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            Debug.Log("OnUpdate AttackExecutionSystem");
            _posRO.Update(ref state);
            var now = (float)SystemAPI.Time.ElapsedTime;
            var em  = state.EntityManager;

            var entities = _attackQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Debug.Log("Processing AttackRequest:");
                var e   = entities[i];
                var req = em.GetComponentData<AttackRequest>(e);
                if (req.HasValue == 0 || req.Target == Entity.Null)
                    continue;
             
                var brain = UnitBrainRegistry.Get(e);
                if (brain == null)
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                // Cooldown (ECS time)
                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime)
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                // Valid positions / in range precheck
                if (!_posRO.HasComponent(e) || !_posRO.HasComponent(req.Target))
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                float3 selfPos = _posRO[e].Position;
                float3 tgtPos  = _posRO[req.Target].Position;

                float range   = math.max(0.01f, brain.UnitDefinition.attackRange);
                float rangeSq = range * range;

                if (math.lengthsq(selfPos - tgtPos) > rangeSq * 1.1f)
                {
                    Consume(ref em, ref req, e);
                    continue;
                }

                // Aim visuals on Mono
                var tgtGO = UnitBrainRegistry.GetGameObject(req.Target);
                if (tgtGO != null) brain.AimAtTarget(tgtGO.transform);

                float3 forward = math.normalizesafe(math.mul(_posRO[e].Rotation, new float3(0, 0, 1)));

                switch (brain.UnitDefinition.combatStrategy)
                {
                    case CombatStrategyType.Melee:
                    {
                        var halfRad = math.radians(math.clamp(brain.UnitDefinition.meleeHalfAngleDeg, 0f, 179f));
                        var invuln  = math.max(0f, brain.UnitDefinition.meleeInvincibility);
                        var maxT    = math.max(1, brain.UnitDefinition.meleeMaxTargets);

                        var hit = new MeleeHitRequest
                        {
                            Origin        = selfPos,
                            Forward       = forward,
                            Range         = range,
                            HalfAngleRad  = halfRad,
                            Damage        = math.max(1f, brain.UnitDefinition.attackDamage),
                            Invincibility = invuln,
                            LayerMask     = brain.GetTargetLayerMask().value,
                            MaxTargets    = maxT,
                            HasValue      = 1
                        };
                        if (em.HasComponent<MeleeHitRequest>(e)) em.SetComponentData(e, hit);
                        else em.AddComponentData(e, hit);
                        break;
                    }

                    case CombatStrategyType.Ranged:
                    {
                        float muzzleFwd = math.max(0f, brain.UnitDefinition.rangedMuzzleForward);
                        var spawn = new EcsProjectileSpawnRequest
                        {
                            Origin    = selfPos + forward * muzzleFwd,
                            Direction = forward,
                            HasValue  = 1
                        };
                        if (em.HasComponent<EcsProjectileSpawnRequest>(e)) em.SetComponentData(e, spawn);
                        else em.AddComponentData(e, spawn);
                        break;
                    }
                }

                cd.NextTime = now + math.max(0.01f, brain.UnitDefinition.attackCooldown);
                if (em.HasComponent<AttackCooldown>(e)) em.SetComponentData(e, cd);
                else em.AddComponentData(e, cd);

                brain.NextAllowedAttackTime = UnityEngine.Time.time + brain.UnitDefinition.attackCooldown;

                Consume(ref em, ref req, e);
            }

            entities.Dispose();
        }

        private static void Consume(ref EntityManager em, ref AttackRequest req, Entity e)
        {
            req.HasValue = 0;
            req.Target   = Entity.Null;
            em.SetComponentData(e, req);
        }
    }
}
