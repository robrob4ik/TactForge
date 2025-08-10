namespace OneBitRob.ECS
{
    using Unity.Entities;
    using Unity.Mathematics;

    public struct AgentTag : IComponentData { }
    public struct AllyTag  : IComponentData { }
    public struct EnemyTag : IComponentData { }

    /// Current target entity.
    public struct Target : IComponentData
    {
        public Entity Value;
    }

    /// Desired destination for nav; consumed by MonoBridge.
    public struct DesiredDestination : IComponentData
    {
        public float3 Position;
        public byte   HasValue;
    }

    public struct DesiredFacing : IComponentData
    {
        public float3 TargetPosition;
        public byte   HasValue;
    }

    /// Pure ECS flag updated each frame so BT/logic doesn't need Mono calls.
    public struct InAttackRange : IComponentData
    {
        public byte  Value;       // 1 = in range, 0 = not
        public float DistanceSq;  // optional debug/telemetry
    }

    /// Mirrored from Mono/Health.
    public struct Alive : IComponentData
    {
        public byte Value; // 1 = alive, 0 = dead
    }

    /// Stored once at spawn (0 none, 1 melee, 2 ranged).
    public struct CombatStyle : IComponentData
    {
        public byte Value;
    }

    /// Mirrored from Mono spell ability each frame.
    public struct SpellState : IComponentData
    {
        public byte CanCast; // true if has a spell & allowed
        public byte Ready;   // true if off cooldown etc.
    }

    public struct AttackRequest : IComponentData
    {
        public Entity Target;
        public byte   HasValue;
    }

    public enum CastKind : byte
    {
        None = 0,
        SingleTarget = 1,
        MultiTarget  = 2,
        AreaOfEffect = 3
    }

    public struct CastRequest : IComponentData
    {
        public CastKind Kind;
        public Entity   Target;           // for SingleTarget
        public float3   AoEPosition;      // for AreaOfEffect
        public byte     HasValue;
    }

    public struct RetargetCooldown : IComponentData
    {
        public double NextTime;   // world time (seconds)
    }

    // Simple per-entity cooldown gate used by AttackExecutionSystem
    public struct AttackCooldown : IComponentData
    {
        public float NextTime; // in seconds (SystemAPI.Time.ElapsedTime)
    }

    public struct MeleeHitRequest : IComponentData
    {
        public float3 Origin;
        public float3 Forward;           // must be normalized
        public float  Range;             // meters
        public float  HalfAngleRad;      // radians
        public float  Damage;            // plain number
        public float  Invincibility;     // seconds to pass into Health.Damage
        public int    LayerMask;         // physics layers to query
        public int    MaxTargets;        // cap for cleave
        public byte   HasValue;          // 1 = pending
    }

    public struct EcsProjectileSpawnRequest : IComponentData
    {
        public float3 Origin;
        public float3 Direction;
        public int    HasValue;   // 1 = pending, 0 = consumed
    }
}
