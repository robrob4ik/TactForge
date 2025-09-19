// File: OneBitRob/AI/SpellWindupAndFireSystem.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.Core;
using OneBitRob.ECS;
using OneBitRob.FX;
using static Unity.Mathematics.math;
using UnityEngine;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AICastPhaseGroup))]
    public partial struct SpellWindupAndFireSystem : ISystem
    {
        private ComponentLookup<LocalTransform>    _ltRO;
        private ComponentLookup<SpatialHashTarget> _factRO;

        public void OnCreate(ref SystemState state)
        {
            _ltRO   = state.GetComponentLookup<LocalTransform>(true);
            _factRO = state.GetComponentLookup<SpatialHashTarget>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _ltRO.Update(ref state);
            _factRO.Update(ref state);

            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            // Release finished windups
            foreach (var (windRO, entity) in SystemAPI.Query<RefRO<SpellWindup>>().WithEntityAccess())
            {
                var wind = windRO.ValueRO;
                if (wind.Active == 0 || now < wind.ReleaseTime) continue;

                var config = em.GetComponentData<SpellConfig>(entity);
                FireSpell(ref state, ref ecb, entity, in config, in wind, now);

                if (em.HasComponent<SpellCooldown>(entity))
                {
                    var cd = em.GetComponentData<SpellCooldown>(entity);
                    cd.NextTime = now + max(0f, config.Cooldown);
                    ecb.SetComponent(entity, cd);
                }

                var w = em.GetComponentData<SpellWindup>(entity);
                w.Active = 0; ecb.SetComponent(entity, w);
            }

            // Start new windups — consume enableable CastRequest
            foreach (var (castRW, e) in SystemAPI.Query<RefRW<CastRequest>>().WithAll<SpellConfig>().WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<CastRequest>(e)) continue;

                var brain  = UnitBrainRegistry.Get(e);
                var config = em.GetComponentData<SpellConfig>(e);

                var windup = em.HasComponent<SpellWindup>(e) ? em.GetComponentData<SpellWindup>(e) : default;
                windup.Active         = 1;
                windup.ReleaseTime    = now + max(0f, config.CastTime);
                windup.FacingDeadline = windup.ReleaseTime;

                float3 aimPosition = default;
                var req = castRW.ValueRO;
                if (req.Kind == CastKind.AreaOfEffect)
                {
                    aimPosition        = req.AoEPosition;
                    windup.AimPoint    = req.AoEPosition;
                    windup.AimTarget   = Entity.Null;
                    windup.HasAimPoint = 1;
                }
                else if (req.Kind == CastKind.SingleTarget && req.Target != Entity.Null)
                {
                    windup.AimTarget   = req.Target;
                    windup.HasAimPoint = 0;
                    aimPosition = SystemAPI.HasComponent<LocalTransform>(req.Target)
                        ? SystemAPI.GetComponent<LocalTransform>(req.Target).Position
                        : aimPosition;
                }

                if (em.HasComponent<DesiredFacing>(e))
                {
                    var df = em.GetComponentData<DesiredFacing>(e);
                    df.TargetPosition = aimPosition; df.HasValue = 1;
                    ecb.SetComponent(e, df);
                }
                else ecb.AddComponent(e, new DesiredFacing { TargetPosition = aimPosition, HasValue = 1 });

                if (brain != null && brain.UnitDefinition != null)
                {
                    var spells = brain.UnitDefinition.unitSpells;
                    if (spells != null && spells.Count > 0 && spells[0] != null)
                    {
                        var def = spells[0];
                        brain.UnitCombatController?.PlaySpellCompute();
                        float3 casterPos = SystemAPI.HasComponent<LocalTransform>(e) ? SystemAPI.GetComponent<LocalTransform>(e).Position : default;
                        FeedbackService.TryPlay(def.prepareFeedback, brain.transform, casterPos);
                    }
                }

                if (em.HasComponent<SpellWindup>(e)) ecb.SetComponent(e, windup);
                else                                 ecb.AddComponent(e, windup);

                SystemAPI.SetComponentEnabled<CastRequest>(e, false);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void FireSpell(ref SystemState state, ref EntityCommandBuffer ecb, Entity caster, in SpellConfig config, in SpellWindup windup, float now)
        {
            var em = state.EntityManager;
            var brain = UnitBrainRegistry.Get(caster);

            float3 selfPos = SystemAPI.HasComponent<LocalTransform>(caster) ? SystemAPI.GetComponent<LocalTransform>(caster).Position : float3.zero;

            float3 fwd   = new float3(0,0,1);
            float3 up    = new float3(0,1,0);
            float3 right = new float3(1,0,0);
            if (SystemAPI.HasComponent<LocalTransform>(caster))
            {
                var rot = SystemAPI.GetComponent<LocalTransform>(caster).Rotation;
                fwd   = math.normalizesafe(math.mul(rot, new float3(0,0,1)));
                up    = math.normalizesafe(math.mul(rot, new float3(0,1,0)));
                right = math.normalizesafe(math.mul(rot, new float3(1,0,0)));
            }

            var stats = em.HasComponent<UnitRuntimeStats>(caster) ? em.GetComponentData<UnitRuntimeStats>(caster) : UnitRuntimeStats.Defaults;

            float3 aimPos = windup.HasAimPoint != 0
                ? windup.AimPoint
                : (windup.AimTarget != Entity.Null && SystemAPI.HasComponent<LocalTransform>(windup.AimTarget)
                    ? SystemAPI.GetComponent<LocalTransform>(windup.AimTarget).Position
                    : selfPos);

            switch (config.Kind)
            {
                case SpellKind.ProjectileLine:
                {
                    float3 origin = selfPos
                        + fwd   * max(0f, config.MuzzleForward)
                        + right * config.MuzzleLocalOffset.x
                        + up    * config.MuzzleLocalOffset.y
                        + fwd   * config.MuzzleLocalOffset.z;

                    float3 dir = math.normalizesafe(aimPos - origin, fwd); dir.y = 0;

                    int mask = ~0;
                    if (brain != null)
                        mask = config.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    float radius = max(0f, config.ProjectileRadius * max(0.0001f, stats.ProjectileRadiusMult));

                    var spawnRequest = new SpellProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = dir,
                        Speed       = max(0.01f, config.ProjectileSpeed),
                        Damage      = config.EffectType == SpellEffectType.Negative ? max(0f, config.Amount) : -max(0f, config.Amount),
                        MaxDistance = max(0.1f, config.ProjectileMaxDistance),
                        Radius      = radius,
                        ProjectileIdHash = config.ProjectileIdHash,
                        LayerMask   = mask,
                        Pierce      = 1
                    };
                    ecb.SetOrAddAndEnable(em, caster, spawnRequest);
                    break;
                }

                case SpellKind.EffectOverTimeArea:
                {
                    int mask = ~0;
                    if (brain != null)
                        mask = config.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    float areaRadius = config.AreaRadius * max(0.0001f, stats.SpellAoeMult);

                    var area = new DoTArea
                    {
                        Position       = windup.HasAimPoint != 0 ? windup.AimPoint : selfPos,
                        Radius         = max(0f, areaRadius),
                        AmountPerTick  = max(0f, config.Amount),
                        Interval       = max(0.05f, config.TickInterval),
                        Remaining      = max(0f, config.Duration),
                        NextTick       = 0f,
                        Positive       = (byte)(config.EffectType == SpellEffectType.Positive ? 1 : 0),
                        AreaVfxIdHash  = config.AreaVfxIdHash,
                        LayerMask      = mask,
                        VfxYOffset     = max(0f, config.AreaVfxYOffset)
                    };
                    // not enableable; set or add
                    if (em.HasComponent<DoTArea>(caster)) ecb.SetComponent(caster, area);
                    else                                   ecb.AddComponent(caster, area);

                    if (brain != null)
                    {
                        var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                        if (spells != null && spells.Count > 0 && spells[0] != null)
                        {
                            var def = spells[0];
                            FeedbackService.TryPlay(def.aoeImpactFeedback, null, (UnityEngine.Vector3)aimPos);
                        }
                    }
                    break;
                }

                case SpellKind.Chain:
                {
                    float3 origin = selfPos
                        + fwd   * max(0f, config.MuzzleForward)
                        + right * config.MuzzleLocalOffset.x
                        + up    * config.MuzzleLocalOffset.y
                        + fwd   * config.MuzzleLocalOffset.z;

                    var chainRunner = new SpellChainRunner
                    {
                        Remaining        = max(1, config.ChainMaxTargets),
                        Radius           = max(0f, config.ChainRadius),
                        JumpDelay        = max(0f, config.ChainJumpDelay),
                        ProjectileSpeed  = max(0.01f, config.ProjectileSpeed),
                        Amount           = max(0f, config.Amount),
                        Positive         = (byte)(config.EffectType == SpellEffectType.Positive ? 1 : 0),
                        ProjectileIdHash = config.ProjectileIdHash,
                        FromPos          = origin,
                        HasFromPos       = 1,
                        CurrentTarget    = windup.AimTarget,
                        PreviousTarget   = Entity.Null,
                        Caster           = caster,
                        CasterFaction    = SystemAPI.HasComponent<SpatialHashTarget>(caster) ? SystemAPI.GetComponent<SpatialHashTarget>(caster).Faction : (byte)0,
                        LayerMask        = (brain != null
                            ? (config.EffectType == SpellEffectType.Positive ? brain.GetFriendlyLayerMask().value : brain.GetDamageableLayerMask().value)
                            : ~0)
                    };
                    if (em.HasComponent<SpellChainRunner>(caster)) ecb.SetComponent(caster, chainRunner);
                    else                                           ecb.AddComponent(caster, chainRunner);
                    break;
                }

                case SpellKind.Summon:
                {
                    var summon = new SummonRequest
                    {
                        PrefabIdHash = config.SummonPrefabHash,
                        Position     = aimPos,
                        Count        = 1,
                        Faction      = SystemAPI.HasComponent<SpatialHashTarget>(caster) ? SystemAPI.GetComponent<SpatialHashTarget>(caster).Faction : (byte)0
                    };
                    ecb.SetOrAddAndEnable(em, caster, summon);
                    break;
                }
            }

            // Post-cast weapon lock
            if (config.PostCastAttackLockSeconds > 0f)
            {
                var lockUntil = new ActionLockUntil { Until = now + config.PostCastAttackLockSeconds };
                if (em.HasComponent<ActionLockUntil>(caster)) ecb.SetComponent(caster, lockUntil);
                else                                          ecb.AddComponent(caster, lockUntil);
            }

            if (brain != null)
            {
                var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                if (spells != null && spells.Count > 0 && spells[0] != null)
                {
                    var def = spells[0];
                    FeedbackService.TryPlay(def.fireFeedback, brain.transform, (UnityEngine.Vector3)selfPos);
                }
            }
        }
    }
}
