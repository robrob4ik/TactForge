// UnitStaticsBootstrap.cs (new)
using OneBitRob.AI;
using OneBitRob.Config;
using OneBitRob.Constants;
using OneBitRob.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    public static class UnitStaticSetup
    {
        public static void Apply(EntityManager em, Entity e, UnitBrain brain)
        {
            var def = brain?.UnitDefinition;
            if (def == null) return;

            byte style = (byte)(def.weapon is RangedWeaponDefinition ? 2 : 1);

            var us = new UnitStatic
            {
                IsEnemy               = (byte)(def.isEnemy ? 1 : 0),
                Faction               = (byte)(def.isEnemy ? GameConstants.ENEMY_FACTION : GameConstants.ALLY_FACTION),
                CombatStyle           = style,
                Layers = new UnitLayerMasks
                {
                    FriendlyMask         = CombatLayers.FriendlyMaskFor(def.isEnemy).value,
                    HostileMask          = CombatLayers.HostileMaskFor(def.isEnemy).value,
                    TargetMask           = CombatLayers.TargetMaskFor(def.isEnemy).value,
                    DamageableLayerIndex = CombatLayers.FactionLayerIndexFor(def.isEnemy)
                },
                Retarget = new RetargetingSettings
                {
                    AutoSwitchMinDistance    = Mathf.Max(0f, def.autoTargetMinSwitchDistance),
                    RetargetCheckInterval    = Mathf.Max(0f, def.retargetCheckInterval),
                    MoveRecheckYieldInterval = Mathf.Max(0f, def.moveRecheckYieldInterval)
                },
                AttackRangeBase      = def.weapon != null ? Mathf.Max(0.01f, def.weapon.attackRange) : 1.5f,
                StoppingDistance     = Mathf.Max(0f, def.stoppingDistance),
                TargetDetectionRange = Mathf.Max(0f, def.targetDetectionRange)
            };
            em.SetOrAdd(e, us);

            var uws = new UnitWeaponStatic { CombatStyle = style };
            if (def.weapon is MeleeWeaponDefinition melee)
            {
                uws.BaseDamage                = Mathf.Max(0f, melee.attackDamage);
                uws.MeleeHalfAngleDeg         = Mathf.Clamp(melee.halfAngleDeg, 0f, 179f);
                uws.MeleeInvincibility        = Mathf.Max(0f, melee.invincibility);
                uws.MeleeMaxTargets           = Mathf.Max(1, melee.maxTargets);
                uws.MeleeSwingLockSeconds     = Mathf.Max(0f, melee.swingLockSeconds);
                uws.MeleeAttackCooldown       = Mathf.Max(0.01f, melee.attackCooldown);
                uws.MeleeAttackCooldownJitter = Mathf.Max(0f, melee.attackCooldownJitter);
                uws.MeleeCritChanceBase       = Mathf.Clamp01(melee.critChance);
                uws.MeleeCritMultiplierBase   = Mathf.Max(1f, melee.critMultiplier);
            }
            else if (def.weapon is RangedWeaponDefinition ranged)
            {
                uws.BaseDamage                   = Mathf.Max(0f, ranged.attackDamage);
                uws.RangedProjectileSpeed        = Mathf.Max(0.01f, ranged.projectileSpeed);
                uws.RangedProjectileMaxDistance  = Mathf.Max(0.1f,  ranged.projectileMaxDistance);
                uws.MuzzleLocalOffset            = new float3(ranged.muzzleLocalOffset.x, ranged.muzzleLocalOffset.y, ranged.muzzleLocalOffset.z);
                uws.MuzzleForward                = Mathf.Max(0f, ranged.muzzleForward);
                uws.RangedAttackCooldown         = Mathf.Max(0.01f, ranged.attackCooldown);
                uws.RangedAttackCooldownJitter   = Mathf.Max(0f,    ranged.attackCooldownJitter);
                uws.RangedWindupSeconds          = Mathf.Max(0f,    ranged.windupSeconds);
                uws.RangedCritChanceBase         = Mathf.Clamp01(ranged.critChance);
                uws.RangedCritMultiplierBase     = Mathf.Max(1f,    ranged.critMultiplier);
            }
            em.SetOrAdd(e, uws);
        }
    }
}
