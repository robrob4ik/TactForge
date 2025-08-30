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
    public partial struct RangedAttackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _ltRO;
        private EntityQuery _reqQ;
        private EntityQuery _windupQ;

        public void OnCreate(ref SystemState state)
        {
            _ltRO  = state.GetComponentLookup<LocalTransform>(true);
            _reqQ  = state.GetEntityQuery(ComponentType.ReadWrite<AttackRequest>());
            _windupQ = state.GetEntityQuery(ComponentType.ReadWrite<AttackWindup>());

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

            ReleaseFinishedWindups_Ranged(em, ref ecb, now);
            ConsumeNewRequests_Ranged(em, ref ecb, now);

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ReleaseFinishedWindups_Ranged(EntityManager em, ref EntityCommandBuffer ecb, float now)
        {
            using var ents = _windupQ.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var w = em.GetComponentData<AttackWindup>(e);
                if (w.Active == 0 || now < w.ReleaseTime) continue;

                // Spell w toku → czekamy.
                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                    continue;

                var brain = UnitBrainRegistry.Get(e);
                var weaponDef = brain?.UnitDefinition?.weapon;
                // Brak możliwości działania → wyczyść windup i dalej.
                if (brain?.UnitCombatController == null || !brain.UnitCombatController.IsAlive || !_ltRO.HasComponent(e))
                {
                    w.Active = 0;
                    ecb.SetComponent(e, w);
                    continue;
                }

                // Obsługujemy tylko ranged.
                if (weaponDef is not RangedWeaponDefinition ranged) { /* melee zrobi inny system */ continue; }

                var lt   = _ltRO[e];
                var pos  = lt.Position;
                var rot  = lt.Rotation;
                var fwd  = math.normalizesafe(math.mul(rot, new float3(0,0,1)));
                var up   = math.normalizesafe(math.mul(rot, new float3(0,1,0)));
                var right= math.normalizesafe(math.mul(rot, new float3(1,0,0)));

                var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                FireRanged(em, ref ecb, e, brain, in ranged, in stats, pos, fwd, right, up, now);

                w.Active = 0;
                ecb.SetComponent(e, w);
            }
        }

        private void ConsumeNewRequests_Ranged(EntityManager em, ref EntityCommandBuffer ecb, float now)
        {
            using var ents = _reqQ.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e   = ents[i];
                var req = em.GetComponentData<AttackRequest>(e);
                if (req.HasValue == 0 || req.Target == Entity.Null) { Consume(ref ecb, e); continue; }

                // Spell w toku → nie startujemy nowej akcji broni.
                if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0)
                { Consume(ref ecb, e); continue; }

                var brain  = UnitBrainRegistry.Get(e);
                var weapon = brain?.UnitDefinition?.weapon;
                if (weapon is not RangedWeaponDefinition ranged) { /* nie nasz typ */ continue; }

                var stats  = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                var cd = em.HasComponent<AttackCooldown>(e) ? em.GetComponentData<AttackCooldown>(e) : default;
                if (now < cd.NextTime) { Consume(ref ecb, e); continue; }

                if (!_ltRO.HasComponent(e) || !em.HasComponent<LocalTransform>(req.Target))
                { Consume(ref ecb, e); continue; }

                var selfLT   = _ltRO[e];
                var targetLT = em.GetComponentData<LocalTransform>(req.Target);

                float baseRange = max(0.01f, weapon.attackRange);
                float effective = baseRange * max(0.0001f, stats.AttackRangeMult_Ranged);
                if (math.distancesq(selfLT.Position, targetLT.Position) > (effective*effective) * 1.1f)
                { Consume(ref ecb, e); continue; }

                var forward = math.normalizesafe(math.mul(selfLT.Rotation, new float3(0,0,1)));
                StartRangedWindup(em, ref ecb, e, brain, in ranged, in stats, selfLT.Position, forward, now);

                Consume(ref ecb, e);
            }
        }

        private static void StartRangedWindup(EntityManager em, ref EntityCommandBuffer ecb, Entity e, UnitBrain brain,
                                              in RangedWeaponDefinition ranged, in UnitRuntimeStats stats,
                                              float3 selfPos, float3 forward, float now)
        {
            if (!em.HasComponent<AttackWindup>(e)) ecb.AddComponent(e, new AttackWindup { Active = 0, ReleaseTime = 0 });

            var wind = em.GetComponentData<AttackWindup>(e);
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

        private static void FireRanged(EntityManager em, ref EntityCommandBuffer ecb, Entity e, UnitBrain brain,
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
                    aimDir = math.normalizesafe(raw, forward);
                }
            }

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

            var cd = ComputeAttackCooldown(ranged.attackCooldown, ranged.attackCooldownJitter, stats.RangedAttackSpeedMult, e, now);
            ecb.SetOrAdd(em, e, cd);
            brain.NextAllowedAttackTime = Time.time + (cd.NextTime - now);
        }
    }
}
