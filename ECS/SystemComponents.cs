// FILE: OneBitRob/ECS/EcsComponents.cs
namespace OneBitRob.ECS
{
    using Unity.Entities;
    using Unity.Mathematics;
    using OneBitRob; // for Spell enums

    public struct AgentTag : IComponentData { }
    public struct AllyTag : IComponentData { }
    public struct EnemyTag : IComponentData { }

    public struct Target : IComponentData
    {
        public Entity Value;
    }

    public struct DesiredDestination : IComponentData
    {
        public float3 Position;
        public byte HasValue;
    }

    public struct DesiredFacing : IComponentData
    {
        public float3 TargetPosition;
        public byte HasValue;
    }

    public struct InAttackRange : IComponentData
    {
        public byte Value;
        public float DistanceSq;
    }

    public struct Alive : IComponentData
    {
        public byte Value;
    }

    public struct CombatStyle : IComponentData
    {
        public byte Value;
    }

    public struct SpellState : IComponentData
    {
        public byte CanCast;
        public byte Ready;
    }

    public struct AttackRequest : IComponentData
    {
        public Entity Target;
        public byte HasValue;
    }

    public struct AttackCooldown : IComponentData
    {
        public float NextTime;
    }

    /// <summary>
    /// For ranged (two‑stage) and optionally melee in future.
    /// When Active==1 and Time >= ReleaseTime, the attack "fires".
    /// </summary>
    public struct AttackWindup : IComponentData
    {
        public float ReleaseTime;
        public byte Active; // 1 = waiting to release
    }

    public enum CastKind : byte
    {
        None = 0,
        SingleTarget = 1,
        MultiTarget = 2,
        AreaOfEffect = 3
    }

    public struct CastRequest : IComponentData
    {
        public CastKind Kind;
        public Entity Target;
        public float3 AoEPosition;
        public byte HasValue;
    }

    public struct RetargetCooldown : IComponentData
    {
        public double NextTime;
    }

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
        public byte HasValue;
    }

    public struct EcsProjectileSpawnRequest : IComponentData
    {
        public float3 Origin;
        public float3 Direction;
        public float Speed;
        public float Damage;
        public float MaxDistance;
        public int HasValue;
    }

    // ───────────────────────────── New (for Burst-able spell decisions)

    /// <summary>Mirror of current/max HP so ECS can query health without Mono.</summary>
    public struct HealthMirror : IComponentData
    {
        public float Current;
        public float Max;
    }

    /// <summary>Baked from the unit's first SpellDefinition (KISS) at spawn.</summary>
    public struct SpellConfig : IComponentData
    {
        public SpellTargetType TargetType;
        public SpellEffectType EffectType; // not used in decision right now
        public float Range;
        public byte RequiresLineOfSight;
        public int TargetLayerMask;
        public float AreaRadius;
        public int MaxTargets;
        public float ChainJumpDelay;
        public SpellTargetingStrategyType Strategy;
    }

    /// <summary>BT trigger: "we want to cast now". SpellDecisionSystem consumes this.</summary>
    public struct SpellDecisionRequest : IComponentData
    {
        public byte HasValue;
    }
}
