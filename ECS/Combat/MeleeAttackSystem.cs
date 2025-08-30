using OneBitRob.ECS;
using OneBitRob.FX;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;
using static OneBitRob.AI.WeaponAttackCommon;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AttackTargetSystem))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial struct MeleeAttackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _ltRO;
        private EntityQuery _reqQ;
        private EntityQuery _windupQ;

        public void OnCreate(ref SystemState state)
        {
            _ltRO   = state.GetComponentLookup<LocalTransform>(true);
            _reqQ   = state.GetEntityQuery(ComponentType.ReadWrite<AttackRequest>());
            _windupQ= state.GetEntityQuery(ComponentType.ReadWrite<AttackWindup>());

            state.RequireForUpdate(state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<AttackRequest>(), ComponentType.ReadOnly<AttackWindup>() }
            }));
        }

        public void OnUpdate(ref SystemState state)
        {
            _ltRO.Update(ref state);

            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            ReleaseFinishedWindups_Melee(em, ref ecb, now);
            ConsumeNewRequests_Melee(em, ref ecb, now);

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ReleaseFinishedWindups_Melee(EntityManager em, ref EntityCommandBuffer ecb, float now)
        {
            using var ents = _windupQ.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var w = em.GetComponentData<AttackWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                    continue;

                var brain = UnitBrainRegistry.Get(e);
                var weaponDef = brain?.UnitDefinition?.weapon;

                if (brain?.UnitCombatController == null || !brain.UnitCombatController.IsAlive || !_ltRO.HasComponent(e))
                {
                    w.Active = 0;
                    ecb.SetComponent(e, w);
                    continue;
                }

                if (weaponDef is not MeleeWeaponDefinition melee) { /* ranged obsłuży inny system */ continue; }

                var lt  = _ltRO[e];
                var pos = lt.Position;
                var fwd = math.normalizesafe(math.mul(lt.Rotation, new float3(0,0,1)));
                var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                // Traf i ustaw cooldown – jak w oryginalnym FinishMelee.
                var hit = BuildMeleeHitRequest(e, brain, in melee, in stats, pos, fwd);
                ecb.SetOrAdd(em, e, hit);

                brain.UnitCombatController?.PlayMeleeAttack(melee.attackAnimations);
                FeedbackService.TryPlay(melee.attackFeedback, brain.transform, (Vector3)pos);

                var cd = ComputeAttackCooldown(melee.attackCooldown, melee.attackCooldownJitter, stats.MeleeAttackSpeedMult, e, now);
                ecb.SetOrAdd(em, e, cd);
                brain.NextAllowedAttackTime = Time.time + (cd.NextTime - now);

                w.Active = 0;
                ecb.SetComponent(e, w);
            }
        }

        private void ConsumeNewRequests_Melee(EntityManager em, ref EntityCommandBuffer ecb, float now)
        {
            using var ents = _reqQ.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e   = ents[i];
                var req = em.GetComponentData<AttackRequest>(e);
                if (req.HasValue == 0 || req.Target == Entity.Null) { Consume(ref ecb, e); continue; }

                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                { Consume(ref ecb, e); continue; }

                var brain  = UnitBrainRegistry.Get(e);
                var weapon = brain?.UnitDefinition?.weapon;
                if (weapon is not MeleeWeaponDefinition melee) { /* nie nasz typ */ continue; }

                var stats  = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime) { Consume(ref ecb, e); continue; }

                if (!_ltRO.HasComponent(e) || !em.HasComponent<LocalTransform>(req.Target))
                { Consume(ref ecb, e); continue; }

                var selfLT   = _ltRO[e];
                var targetLT = em.GetComponentData<LocalTransform>(req.Target);

                float baseRange = max(0.01f, weapon.attackRange);
                float effective = baseRange * max(0.0001f, stats.AttackRangeMult_Melee);
                if (math.distancesq(selfLT.Position, targetLT.Position) > (effective*effective) * 1.1f)
                { Consume(ref ecb, e); continue; }

                var forward = math.normalizesafe(math.mul(selfLT.Rotation, new float3(0,0,1)));

                // Natychmiastowe trafienie – zgodnie z oryginałem BEZ ustawiania cooldownu tutaj.
                var hit = BuildMeleeHitRequest(e, brain, in melee, in stats, selfLT.Position, forward);
                ecb.SetOrAdd(em, e, hit);

                brain.UnitCombatController?.PlayMeleeAttack(melee.attackAnimations);
                FeedbackService.TryPlay(melee.attackFeedback, brain.transform, (Vector3)selfLT.Position);

                Consume(ref ecb, e);
            }
        }
    }
}
