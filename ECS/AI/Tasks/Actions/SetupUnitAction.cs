using OneBitRob.Config;
using OneBitRob.Constants;
using OneBitRob.Core;
using OneBitRob.ECS;
using OneBitRob.VFX;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Basic setup of EnigmaEngine character")]
    public class SetupUnitAction : AbstractTaskAction<SetupUnitComponent, SetupUnitTag, SetupUnitSystem>, IAction
    {
        protected override SetupUnitComponent CreateBufferElement(ushort runtimeIndex) { return new SetupUnitComponent { Index = runtimeIndex }; }
    }

    public struct SetupUnitComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct SetupUnitTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class SetupUnitSystem : TaskProcessorSystem<SetupUnitComponent, SetupUnitTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var em = EntityManager;

            brain.Setup();

            // ── Health mirror
            int hp = 100;
            var def = brain != null ? brain.UnitDefinition : null;
            if (def != null) hp = def.health;

            var hm = new HealthMirror { Current = hp, Max = hp };
            em.SetOrAdd(e, hm);

            // ── Combat style from weapon type (1 = melee, 2 = ranged)
            byte style = 1;
            var weapon = def != null ? def.weapon : null;
            if (weapon is RangedWeaponDefinition) style = 2;

            em.SetOrAdd(e, new CombatStyle { Value = style });

            // ── Retarget assist scaffolding
            float3 spawnPos = em.HasComponent<LocalTransform>(e) ? em.GetComponentData<LocalTransform>(e).Position : float3.zero;

            if (em.HasComponent<RetargetAssist>(e))
            {
                var ra = em.GetComponentData<RetargetAssist>(e);
                ra.LastPos        = spawnPos;
                ra.LastDistSq     = float.MaxValue;
                ra.NoProgressTime = 0f;
                em.SetComponentData(e, ra);
            }
            else
            {
                em.AddComponentData(e, new RetargetAssist
                {
                    LastPos        = spawnPos,
                    LastDistSq     = float.MaxValue,
                    NoProgressTime = 0f
                });
            }
            if (!em.HasComponent<RetargetCooldown>(e)) em.AddComponentData(e, new RetargetCooldown { NextTime = 0 });

            // ── Spell baseline (first slot only, unchanged logic)
            var spells = def != null ? def.unitSpells : null;
            bool hasSpell = spells != null && spells.Count > 0 && spells[0] != null;
            if (hasSpell)
            {
                var spell      = spells[0];
                int projHash   = VisualAssetRegistry.RegisterProjectile(spell.ProjectileId);
                int effectHash = VisualAssetRegistry.RegisterVfx(spell.EffectVfxId);
                int areaVfxHash= VisualAssetRegistry.RegisterVfx(spell.AreaVfxId);
                int summonHash = VisualAssetRegistry.RegisterSummon(spell.SummonPrefab);

                float amount = (spell.Kind == SpellKind.EffectOverTimeArea || spell.Kind == SpellKind.EffectOverTimeTarget)
                               ? spell.TickAmount : spell.EffectAmount;

                var config = new SpellConfig
                {
                    Kind          = spell.Kind,
                    EffectType    = spell.EffectType,
                    AcquireMode   = spell.AcquireMode,

                    CastTime      = spell.FireDelaySeconds,
                    Cooldown      = spell.Cooldown,
                    Range         = spell.Range,

                    Amount        = amount,

                    ProjectileSpeed       = spell.ProjectileSpeed,
                    ProjectileMaxDistance = spell.ProjectileMaxDistance,
                    ProjectileRadius      = spell.ProjectileRadius,
                    ProjectileIdHash      = projHash,
                    MuzzleForward         = spell.MuzzleForward,
                    MuzzleLocalOffset     = new float3(spell.MuzzleLocalOffset.x, spell.MuzzleLocalOffset.y, spell.MuzzleLocalOffset.z),

                    AreaRadius     = (spell.Kind == SpellKind.EffectOverTimeArea ? spell.AreaRadius : 0f),
                    Duration       = spell.Duration,
                    TickInterval   = spell.TickInterval,
                    EffectVfxIdHash= effectHash,
                    AreaVfxIdHash  = areaVfxHash,
                    AreaVfxYOffset = spell.AreaVfxYOffset,

                    ChainMaxTargets= spell.ChainMaxTargets,
                    ChainRadius    = spell.ChainRadius,
                    ChainJumpDelay = spell.ChainPerJumpDelay,

                    SummonPrefabHash= summonHash,

                    PostCastAttackLockSeconds = Mathf.Max(0f, spell.PostCastAttackLockSeconds)
                };

                em.SetOrAdd(e, config);

                if (!em.HasComponent<SpellDecisionRequest>(e)) em.AddComponent<SpellDecisionRequest>(e);
                if (!em.HasComponent<SpellWindup>(e))          em.AddComponentData(e, new SpellWindup { Active = 0, ReleaseTime = 0f });
                if (!em.HasComponent<SpellCooldown>(e))        em.AddComponentData(e, new SpellCooldown { NextTime = 0f });

                if (em.HasComponent<SpellState>(e))
                {
                    var ss = em.GetComponentData<SpellState>(e);
                    if (ss.CanCast == 0 || ss.Ready == 0)
                    {
                        ss.CanCast = 1; ss.Ready = 1;
                        em.SetComponentData(e, ss);
                    }
                }
            }

            // ── Stats scaffolding (unchanged)
            if (!em.HasComponent<UnitRuntimeStats>(e)) em.AddComponentData(e, UnitRuntimeStats.Defaults);
            if (!em.HasBuffer<StatModifier>(e))        em.AddBuffer<StatModifier>(e);
            if (!em.HasComponent<StatsDirtyTag>(e))    em.AddComponent<StatsDirtyTag>(e);
            if (def != null && def.baseScaling != null) StatsService.AddModifiers(e, def.baseScaling);

            // ── UnitStatic consolidated gameplay tunables
            var us = new UnitStatic
            {
                IsEnemy               = (byte)(def != null && def.isEnemy ? 1 : 0),
                Faction               = (byte)((def != null && def.isEnemy) ? GameConstants.ENEMY_FACTION : GameConstants.ALLY_FACTION),
                CombatStyle           = style,

                Layers = new UnitLayerMasks
                {
                    FriendlyMask         = CombatLayers.FriendlyMaskFor(def != null && def.isEnemy).value,
                    HostileMask          = CombatLayers.HostileMaskFor(def != null && def.isEnemy).value,
                    TargetMask           = CombatLayers.TargetMaskFor(def != null && def.isEnemy).value,
                    DamageableLayerIndex = CombatLayers.FactionLayerIndexFor(def != null && def.isEnemy)
                },

                Retarget = new RetargetingSettings
                {
                    AutoSwitchMinDistance    = def != null ? Mathf.Max(0f, def.autoTargetMinSwitchDistance) : 0f,
                    RetargetCheckInterval    = def != null ? Mathf.Max(0f, def.retargetCheckInterval)       : 0f,
                    MoveRecheckYieldInterval = def != null ? Mathf.Max(0f, def.moveRecheckYieldInterval)    : 0f
                },

                AttackRangeBase      = weapon != null ? Mathf.Max(0.01f, weapon.attackRange) : 1.5f,
                StoppingDistance     = def != null    ? Mathf.Max(0f, def.stoppingDistance) : 1.5f,
                TargetDetectionRange = def != null    ? Mathf.Max(0f, def.targetDetectionRange) : 100f
            };
            em.SetOrAdd(e, us);

            // ── NEW: UnitWeaponStatic (formalized weapon gameplay tunables)
            var uws = new UnitWeaponStatic
            {
                CombatStyle = style,
            };

            if (weapon is MeleeWeaponDefinition melee)
            {
                uws.BaseDamage                 = Mathf.Max(0f, melee.attackDamage);
                uws.MeleeHalfAngleDeg          = Mathf.Clamp(melee.halfAngleDeg, 0f, 179f);
                uws.MeleeInvincibility         = Mathf.Max(0f, melee.invincibility);
                uws.MeleeMaxTargets            = Mathf.Max(1,  melee.maxTargets);
                uws.MeleeSwingLockSeconds      = Mathf.Max(0f, melee.swingLockSeconds);

                uws.MeleeAttackCooldown        = Mathf.Max(0.01f, melee.attackCooldown);
                uws.MeleeAttackCooldownJitter  = Mathf.Max(0f, melee.attackCooldownJitter);

                uws.MeleeCritChanceBase        = Mathf.Clamp01(melee.critChance);
                uws.MeleeCritMultiplierBase    = Mathf.Max(1f,   melee.critMultiplier);
            }
            else if (weapon is RangedWeaponDefinition ranged)
            {
                uws.BaseDamage                    = Mathf.Max(0f, ranged.attackDamage);
                uws.RangedProjectileSpeed         = Mathf.Max(0.01f, ranged.projectileSpeed);
                uws.RangedProjectileMaxDistance   = Mathf.Max(0.1f,  ranged.projectileMaxDistance);
                uws.MuzzleLocalOffset             = new float3(ranged.muzzleLocalOffset.x, ranged.muzzleLocalOffset.y, ranged.muzzleLocalOffset.z);
                uws.MuzzleForward                 = Mathf.Max(0f, ranged.muzzleForward);

                uws.RangedAttackCooldown          = Mathf.Max(0.01f, ranged.attackCooldown);
                uws.RangedAttackCooldownJitter    = Mathf.Max(0f,    ranged.attackCooldownJitter);
                uws.RangedWindupSeconds           = Mathf.Max(0f,    ranged.windupSeconds);

                uws.RangedCritChanceBase          = Mathf.Clamp01(ranged.critChance);
                uws.RangedCritMultiplierBase      = Mathf.Max(1f,    ranged.critMultiplier);
            }

            em.SetOrAdd(e, uws);

            return TaskStatus.Success;
        }
    }
}