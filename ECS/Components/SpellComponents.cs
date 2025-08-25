// FILE: Assets/PROJECT/Scripts/Runtime/ECS/Components/SpellComponents.cs
using System;
using Unity.Entities;
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    public enum CastKind : byte { None = 0, SingleTarget = 1, AreaOfEffect = 3 }

    public struct CastRequest : IComponentData
    {
        public CastKind Kind;
        public Entity Target;
        public float3 AoEPosition;
        public byte HasValue;
    }

    public struct SpellState : IComponentData { public byte CanCast; public byte Ready; }

    public struct SpellConfig : IComponentData
    {
        public OneBitRob.SpellKind Kind;
        public OneBitRob.SpellEffectType EffectType;
        public OneBitRob.SpellAcquireMode AcquireMode;

        public float CastTime;
        public float Cooldown;
        public float Range;
        public byte  RequiresLineOfSight;
        public int   TargetLayerMask;

        public byte  RequireFacing;
        public float FaceToleranceDeg;
        public float MaxExtraFaceDelay;

        public float Amount;

        // Projectile
        public float ProjectileSpeed;
        public float ProjectileMaxDistance;
        public float ProjectileRadius;
        public int   ProjectileIdHash;
        public float MuzzleForward;
        public float3 MuzzleLocalOffset;

        // AOE / Over-Time
        public float AreaRadius;
        public float Duration;
        public float TickInterval;
        public int   EffectVfxIdHash;
        public int   AreaVfxIdHash;
        public float AreaVfxYOffset;     // <-- NEW: VFX-only vertical offset

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
        public byte  Active;

        public float3 AimPoint;
        public Entity AimTarget;
        public byte  HasAimPoint;

        public float FacingDeadline;
    }

    public struct SpellCooldown : IComponentData { public float NextTime; }

    public struct SpellProjectileSpawnRequest : IComponentData
    {
        public float3 Origin;
        public float3 Direction;
        public float  Speed;
        public float  Damage;
        public float  MaxDistance;
        public float  Radius;
        public int    ProjectileIdHash;
        public int    LayerMask;
        public byte   Pierce;
        public int    HasValue;
    }

    public struct SummonRequest : IComponentData
    {
        public int   PrefabIdHash;
        public float3 Position;
        public byte  Count;
        public byte  Faction;
        public int   HasValue;
    }

    public struct DotOnTarget : IComponentData
    {
        public Entity Target;
        public float  AmountPerTick;
        public float  Interval;
        public float  Remaining;
        public float  NextTick;
        public byte   Positive;
        public int    EffectVfxIdHash;
    }

    public struct ActiveAreaVfx : IComponentData { public long Key; public int IdHash; }

    public struct DoTArea : IComponentData
    {
        public float3 Position;
        public float  Radius;
        public float  AmountPerTick;
        public float  Interval;
        public float  Remaining;
        public float  NextTick;
        public byte   Positive;
        public int    AreaVfxIdHash;
        public int    LayerMask;
        public float  VfxYOffset;   // <-- NEW: visuals only, damage stays at Position
    }

    public struct SpellChainRunner : IComponentData
    {
        public int   Remaining;
        public float Radius;
        public float JumpDelay;
        public float ProjectileSpeed;
        public float Amount;
        public byte  Positive;
        public int   ProjectileIdHash;
        public int   LayerMask;

        public Entity CurrentTarget;
        public Entity PreviousTarget;

        public float3 FromPos;
        public byte   HasFromPos;
        public float  NextTime;

        public Entity Caster;
        public byte   CasterFaction;
    }

    public struct ActiveTargetVfx : IComponentData
    {
        public long   Key;
        public int    IdHash;
        public Entity Target;
    }
}
