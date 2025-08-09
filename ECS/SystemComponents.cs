
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    using Unity.Entities;

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
        public byte HasValue;
    }
    
    public struct DesiredFacing : IComponentData
    {
        public float3 TargetPosition;
        public byte   HasValue;
    }

    public struct AttackRequest : IComponentData
    {
        public Entity Target;   // entity we intend to attack
        public byte   HasValue; // 1 when requested this frame
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
        public double NextTime;   // world time (seconds) when retarget is allowed again
    }
}