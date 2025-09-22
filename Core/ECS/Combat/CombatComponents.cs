using Unity.Entities;
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    public struct AttackRequest : IComponentData, IEnableableComponent
    {
        public Entity Target;
    }

    public struct AttackCooldown : IComponentData
    {
        public float NextTime;
    }

    public struct AttackWindup : IComponentData
    {
        public float ReleaseTime;
        public byte Active;
    }

    public struct EcsProjectileSpawnRequest : IComponentData, IEnableableComponent
    {
        public float3 Origin;
        public float3 Direction;
        public float Speed;
        public float Damage;
        public float MaxDistance;

        public float CritChance;
        public float CritMultiplier;

        public float PierceChance;
        public int PierceMaxTargets;
    }

    public struct MeleeHitRequest : IComponentData, IEnableableComponent
    {
        public float3 Origin;
        public float3 Forward;
        public float Range;
        public float HalfAngleRad;
        public float Damage;
        public float Invincibility;
        public int LayerMask;
        public int MaxTargets;
        public float CritChance;
        public float CritMultiplier;
    }

    public struct UnitWeaponStatic : IComponentData
    {
        public byte CombatStyle;

        // Common
        public float BaseDamage;

        // Ranged
        public float RangedProjectileSpeed;
        public float RangedProjectileMaxDistance;
        public float3 MuzzleLocalOffset;
        public float MuzzleForward;
        public float RangedAttackCooldown;
        public float RangedAttackCooldownJitter;
        public float RangedCritChanceBase;
        public float RangedCritMultiplierBase;
        public float RangedWindupSeconds;

        // Melee
        public float MeleeAttackCooldown;
        public float MeleeAttackCooldownJitter;
        public float MeleeHalfAngleDeg;
        public float MeleeInvincibility;
        public int MeleeMaxTargets;
        public float MeleeCritChanceBase;
        public float MeleeCritMultiplierBase;
        public float MeleeSwingLockSeconds;
    }
}