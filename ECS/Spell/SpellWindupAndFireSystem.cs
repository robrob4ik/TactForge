using OneBitRob.ECS;
using OneBitRob.FX;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using UnityEngine;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct SpellWindupAndFireSystem : ISystem
    {
        private EntityQuery _castQuery;
        private EntityQuery _pendingWindupQuery;
        private ComponentLookup<LocalTransform> _transformLookup; 
        private ComponentLookup<SpatialHashTarget> _factionLookup; 

        public void OnCreate(ref SystemState state)
        {
            _castQuery          = state.GetEntityQuery(ComponentType.ReadWrite<CastRequest>(), ComponentType.ReadOnly<SpellConfig>());
            _pendingWindupQuery = state.GetEntityQuery(ComponentType.ReadWrite<SpellWindup>(), ComponentType.ReadOnly<SpellConfig>());
            _transformLookup    = state.GetComponentLookup<LocalTransform>(true);
            _factionLookup      = state.GetComponentLookup<SpatialHashTarget>(true);
            state.RequireForUpdate(_castQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _factionLookup.Update(ref state);

            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            ReleaseFinishedWindups(em, ref ecb, now, ref state);
            StartNewWindups      (em, ref ecb, now);

            ecb.Playback(em);
            ecb.Dispose();
        }

        private void ReleaseFinishedWindups(EntityManager em, ref EntityCommandBuffer ecb, float now, ref SystemState state)
        {
            using var windupEntities = _pendingWindupQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < windupEntities.Length; i++)
            {
                var entity = windupEntities[i];
                var windup = em.GetComponentData<SpellWindup>(entity);
                if (windup.Active == 0 || now < windup.ReleaseTime) continue;

                var config = em.GetComponentData<SpellConfig>(entity);
                FireSpell(ref state, ref ecb, entity, in config, in windup, now);

                if (em.HasComponent<SpellCooldown>(entity))
                {
                    var cd = em.GetComponentData<SpellCooldown>(entity);
                    cd.NextTime = now + math.max(0f, config.Cooldown);
                    ecb.SetComponent(entity, cd);
                }

                windup.Active = 0;
                ecb.SetComponent(entity, windup);
            }
        }

        private void StartNewWindups(EntityManager em, ref EntityCommandBuffer ecb, float now)
        {
            using var castEntities = _castQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < castEntities.Length; i++)
            {
                var entity  = castEntities[i];
                var request = em.GetComponentData<CastRequest>(entity);
                if (request.HasValue == 0) continue;

                var brain  = UnitBrainRegistry.Get(entity);
                var config = em.GetComponentData<SpellConfig>(entity);

                var windup = em.HasComponent<SpellWindup>(entity) ? em.GetComponentData<SpellWindup>(entity) : default;
                windup.Active         = 1;
                windup.ReleaseTime    = now + math.max(0f, config.CastTime);
                windup.FacingDeadline = windup.ReleaseTime;

                // ── Resolve aim and WRITE it into windup ───────────────────────
                float3 aimPosition = default;
                if (request.Kind == CastKind.AreaOfEffect)
                {
                    aimPosition       = request.AoEPosition;
                    windup.AimPoint   = request.AoEPosition;
                    windup.AimTarget  = Entity.Null;
                    windup.HasAimPoint= 1;
                }
                else if (request.Kind == CastKind.SingleTarget && request.Target != Entity.Null)
                {
                    windup.AimTarget   = request.Target;
                    windup.HasAimPoint = 0;
                    aimPosition = _transformLookup.HasComponent(request.Target) ? _transformLookup[request.Target].Position : aimPosition;
                }

                // Face aim immediately
                if (em.HasComponent<DesiredFacing>(entity))
                {
                    var df = em.GetComponentData<DesiredFacing>(entity);
                    df.TargetPosition = aimPosition;
                    df.HasValue = 1;
                    ecb.SetComponent(entity, df);
                }
                else ecb.AddComponent(entity, new DesiredFacing { TargetPosition = aimPosition, HasValue = 1 });

                // Prepare animation/feedback
                if (brain != null && brain.UnitDefinition != null)
                {
                    var spells = brain.UnitDefinition.unitSpells;
                    if (spells != null && spells.Count > 0 && spells[0] != null)
                    {
                        var def = spells[0];
                        brain.UnitCombatController?.PlaySpell(def.animations);
                        float3 casterPos = _transformLookup.HasComponent(entity) ? _transformLookup[entity].Position : default;
                        FeedbackService.TryPlay(def.prepareFeedback, brain.transform, (Vector3)casterPos);
                    }
                }

                // write windup + consume the request
                if (em.HasComponent<SpellWindup>(entity)) ecb.SetComponent(entity, windup);
                else                                      ecb.AddComponent(entity, windup);

                request.HasValue = 0;
                ecb.SetComponent(entity, request);
            }
        }

        private void FireSpell(ref SystemState state, ref EntityCommandBuffer ecb, Entity caster, in SpellConfig config, in SpellWindup windup, float now)
        {
            var em    = state.EntityManager;
            var brain = UnitBrainRegistry.Get(caster);

            float3 selfPos = _transformLookup.HasComponent(caster) ? _transformLookup[caster].Position : float3.zero;

            float3 fwd   = new float3(0,0,1);
            float3 up    = new float3(0,1,0);
            float3 right = new float3(1,0,0);
            if (_transformLookup.HasComponent(caster))
            {
                var rot = _transformLookup[caster].Rotation;
                fwd   = normalizesafe(mul(rot, new float3(0,0,1)));
                up    = normalizesafe(mul(rot, new float3(0,1,0)));
                right = normalizesafe(mul(rot, new float3(1,0,0)));
            }

            var stats = em.HasComponent<UnitRuntimeStats>(caster) ? em.GetComponentData<UnitRuntimeStats>(caster) : UnitRuntimeStats.Defaults;

            float3 aimPos = windup.HasAimPoint != 0
                ? windup.AimPoint
                : (windup.AimTarget != Entity.Null && _transformLookup.HasComponent(windup.AimTarget) ? _transformLookup[windup.AimTarget].Position : selfPos);

            switch (config.Kind)
            {
                case SpellKind.ProjectileLine:
                {
                    float3 origin = selfPos
                        + fwd   * math.max(0f, config.MuzzleForward)
                        + right * config.MuzzleLocalOffset.x
                        + up    * config.MuzzleLocalOffset.y
                        + fwd   * config.MuzzleLocalOffset.z;

                    float3 dir = normalizesafe(aimPos - origin, fwd); dir.y = 0;

                    int mask = ~0;
                    if (brain != null)
                        mask = config.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    float radius = math.max(0f, config.ProjectileRadius * math.max(0.0001f, stats.ProjectileRadiusMult));

                    var spawnRequest = new SpellProjectileSpawnRequest
                    {
                        Origin      = origin,
                        Direction   = dir,
                        Speed       = math.max(0.01f, config.ProjectileSpeed),
                        Damage      = config.EffectType == SpellEffectType.Negative ? math.max(0f, config.Amount) : -math.max(0f, config.Amount),
                        MaxDistance = math.max(0.1f, config.ProjectileMaxDistance),
                        Radius      = radius,
                        ProjectileIdHash = config.ProjectileIdHash,
                        LayerMask   = mask,
                        Pierce      = 1,
                        HasValue    = 1
                    };
                    if (em.HasComponent<SpellProjectileSpawnRequest>(caster)) ecb.SetComponent(caster, spawnRequest);
                    else                                                      ecb.AddComponent(caster, spawnRequest);
                    break;
                }

                case SpellKind.EffectOverTimeArea:
                {
                    int mask = ~0;
                    if (brain != null)
                        mask = config.EffectType == SpellEffectType.Positive
                            ? brain.GetFriendlyLayerMask().value
                            : brain.GetDamageableLayerMask().value;

                    float areaRadius = config.AreaRadius * math.max(0.0001f, stats.SpellAoeMult);

                    var area = new DoTArea
                    {
                        Position       = windup.HasAimPoint != 0 ? windup.AimPoint : (_transformLookup.HasComponent(caster) ? _transformLookup[caster].Position : float3.zero),
                        Radius         = math.max(0f, areaRadius),
                        AmountPerTick  = math.max(0f, config.Amount),
                        Interval       = math.max(0.05f, config.TickInterval),
                        Remaining      = math.max(0f, config.Duration),
                        NextTick       = 0f,
                        Positive       = (byte)(config.EffectType == SpellEffectType.Positive ? 1 : 0),
                        AreaVfxIdHash  = config.AreaVfxIdHash,
                        LayerMask      = mask,
                        VfxYOffset     = math.max(0f, config.AreaVfxYOffset)
                    };
                    if (em.HasComponent<DoTArea>(caster)) ecb.SetComponent(caster, area);
                    else                                   ecb.AddComponent(caster, area);

                    if (brain != null)
                    {
                        var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                        if (spells != null && spells.Count > 0 && spells[0] != null)
                        {
                            var def = spells[0];
                            FeedbackService.TryPlay(def.impactFeedback, null, (Vector3)aimPos);
                        }
                    }
                    break;
                }

                case SpellKind.Chain:
                {
                    float3 origin = selfPos
                        + fwd   * math.max(0f, config.MuzzleForward)
                        + right * config.MuzzleLocalOffset.x
                        + up    * config.MuzzleLocalOffset.y
                        + fwd   * config.MuzzleLocalOffset.z;

                    var chainRunner = new SpellChainRunner
                    {
                        Remaining        = math.max(1, config.ChainMaxTargets),
                        Radius           = math.max(0f, config.ChainRadius),
                        JumpDelay        = math.max(0f, config.ChainJumpDelay),
                        ProjectileSpeed  = math.max(0.01f, config.ProjectileSpeed),
                        Amount           = math.max(0f, config.Amount),
                        Positive         = (byte)(config.EffectType == SpellEffectType.Positive ? 1 : 0),
                        ProjectileIdHash = config.ProjectileIdHash,
                        FromPos          = origin,
                        HasFromPos       = 1,
                        CurrentTarget    = windup.AimTarget,
                        PreviousTarget   = Entity.Null,
                        Caster           = caster,
                        CasterFaction    = SystemAPI.HasComponent<SpatialHashTarget>(caster)
                            ? SystemAPI.GetComponent<SpatialHashTarget>(caster).Faction
                            : (byte)Constants.GameConstants.ALLY_FACTION,
                        LayerMask        = (brain != null
                            ? (config.EffectType == SpellEffectType.Positive
                                ? brain.GetFriendlyLayerMask().value
                                : brain.GetDamageableLayerMask().value)
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
                        Faction      = SystemAPI.HasComponent<SpatialHashTarget>(caster)
                            ? SystemAPI.GetComponent<SpatialHashTarget>(caster).Faction
                            : (byte)0,
                        HasValue     = (config.SummonPrefabHash != 0 ? 1 : 0)
                    };

                    if (summon.HasValue != 0)
                    {
                        if (em.HasComponent<SummonRequest>(caster)) ecb.SetComponent(caster, summon);
                        else                                       ecb.AddComponent(caster, summon);
                    }
                    break;
                }
            }

            // Post-cast attack lock to avoid immediate weapon fire
            if (config.PostCastAttackLockSeconds > 0f)
            {
                var lockUntil = new ActionLockUntil { Until = now + config.PostCastAttackLockSeconds };
                if (em.HasComponent<ActionLockUntil>(caster)) 
                    ecb.SetComponent(caster, lockUntil);
                else                                          
                    ecb.AddComponent(caster, lockUntil);
            }

            if (brain != null)
            {
                var spells = brain.UnitDefinition != null ? brain.UnitDefinition.unitSpells : null;
                if (spells != null && spells.Count > 0 && spells[0] != null)
                {
                    var def = spells[0];
                    FeedbackService.TryPlay(def.fireFeedback, brain.transform, (Vector3)selfPos);
                }
            }
        }
    }
}