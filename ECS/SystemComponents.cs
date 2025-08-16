using Unity.Entities;
using Unity.Mathematics;
using OneBitRob;

namespace OneBitRob.ECS
{
    public struct AgentTag : IComponentData { }
    public struct AllyTag : IComponentData { }
    public struct EnemyTag : IComponentData { }

    public struct Target : IComponentData { public Entity Value; }

    public struct DesiredDestination : IComponentData { public float3 Position; public byte HasValue; }
    public struct DesiredFacing      : IComponentData { public float3 TargetPosition; public byte HasValue; }

    public struct InAttackRange : IComponentData { public byte Value; public float DistanceSq; }
    public struct Alive         : IComponentData { public byte Value; }
    public struct CombatStyle   : IComponentData { public byte Value; }
    public struct SpellState    : IComponentData { public byte CanCast; public byte Ready; }

    public struct AttackRequest  : IComponentData { public Entity Target; public byte HasValue; }
    public struct AttackCooldown : IComponentData { public float NextTime; }
    public struct AttackWindup   : IComponentData { public float ReleaseTime; public byte Active; }

    public enum CastKind : byte { None = 0, SingleTarget = 1, AreaOfEffect = 3 }

    public struct CastRequest : IComponentData
    {
        public CastKind Kind;
        public Entity Target;
        public float3 AoEPosition;
        public byte HasValue;
    }

    public struct RetargetCooldown : IComponentData { public double NextTime; }

    public struct MeleeHitRequest : IComponentData
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
        public byte HasValue;
    }

    public struct EcsProjectileSpawnRequest : IComponentData
    {
        public float3 Origin;
        public float3 Direction;
        public float Speed;
        public float Damage;
        public float MaxDistance;
        public float CritChance;
        public float CritMultiplier;
        public int HasValue;
    }

    public struct HealthMirror : IComponentData { public float Current; public float Max; }

    // ─────────────────────────────────────────────────────────────────────────
    // SPELL MODEL
    public struct SpellConfig : IComponentData
    {
        public SpellKind Kind;
        public SpellEffectType EffectType;
        public SpellAcquireMode AcquireMode;

        public float CastTime;
        public float Cooldown;
        public float Range;
        public byte RequiresLineOfSight;
        public int TargetLayerMask;

        public byte RequireFacing;
        public float FaceToleranceDeg;
        public float MaxExtraFaceDelay;

        public float Amount; // damage or heal per hit/tick

        // Projectile
        public float ProjectileSpeed;
        public float ProjectileMaxDistance;
        public float ProjectileRadius;
        public int   ProjectileIdHash;

        // AoE / Over-Time
        public float AreaRadius;
        public float Duration;
        public float TickInterval;
        public int   EffectVfxIdHash;
        public int   AreaVfxIdHash;

        // Chain
        public int   ChainMaxTargets;
        public float ChainRadius;
        public float ChainJumpDelay;

        // Summon
        public int   SummonPrefabHash;
    }

    public struct SpellDecisionRequest : IComponentData { public byte HasValue; }

    public struct SpellWindup : IComponentData
    {
        public float ReleaseTime;
        public byte Active;

        public float3 AimPoint;
        public Entity AimTarget;
        public byte HasAimPoint;

        public float FacingDeadline;
    }

    public struct SpellCooldown : IComponentData { public float NextTime; }

    public struct SpellProjectileSpawnRequest : IComponentData
    {
        public float3 Origin;
        public float3 Direction;
        public float Speed;
        public float Damage;
        public float MaxDistance;
        public float Radius;
        public int   ProjectileIdHash;
        public int   LayerMask;
        public byte  Pierce;     // 1 = hit all along
        public int   HasValue;
    }

    public struct SummonRequest : IComponentData
    {
        public int   PrefabIdHash; // maps to GameObject via SpellVisualRegistry
        public float3 Position;
        public byte  Count;
        public byte  Faction; // same as SpatialHashTarget.Faction on caster
        public int   HasValue;
    }

    public struct DotOnTarget : IComponentData
    {
        public Entity Target;
        public float AmountPerTick;
        public float Interval;
        public float Remaining;
        public float NextTick;
        public byte  Positive;       // 1=heal, 0=damage
        public int   EffectVfxIdHash;
    }

    public struct DoTArea : IComponentData
    {
        public float3 Position;
        public float Radius;
        public float AmountPerTick;
        public float Interval;
        public float Remaining;
        public float NextTick;
        public byte  Positive;       // 1=heal, 0=damage
        public int   AreaVfxIdHash;
        public int   LayerMask;      // who gets affected
    }

    /// Tracks "no progress" toward current target to trigger retarget assist.
    public struct RetargetAssist : IComponentData
    {
        public float3 LastPos;
        public float LastDistSq;
        public float NoProgressTime;
    }
}
