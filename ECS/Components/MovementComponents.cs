using System;
using Unity.Entities;
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    public struct DesiredDestination : IComponentData { public float3 Position; public byte HasValue; }
    public struct DesiredFacing      : IComponentData { public float3 TargetPosition; public byte HasValue; }

    [Flags]
    public enum MovementLockFlags : byte
    {
        None      = 0,
        Casting   = 1 << 0,
        Rooted    = 1 << 1,
        Stunned   = 1 << 2,
        Attacking = 1 << 3,
    }
    public struct MovementLock : IComponentData { public MovementLockFlags Flags; }

    public struct BehaviorYieldCooldown : IComponentData { public double NextTime; }
    public struct ActionLockUntil      : IComponentData { public float  Until; }
}