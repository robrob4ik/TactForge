using Unity.Entities;
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    public struct AttackRequest  : IComponentData { public Entity Target; public byte HasValue; }
    public struct AttackCooldown : IComponentData { public float NextTime; }
    public struct AttackWindup   : IComponentData { public float ReleaseTime; public byte Active; }

    public struct RetargetCooldown : IComponentData { public double NextTime; }

    public struct MeleeHitRequest : IComponentData
    {
        public float3 Origin;
        public float3 Forward;
        public float Range;
        public float HalfAngleRad;
        public float Damage;
        public float Invincibility;
        public int   LayerMask;
        public int   MaxTargets;
        public float CritChance;
        public float CritMultiplier;
        public byte  HasValue;
    }
    
    public struct EcsProjectileSpawnRequest : IComponentData
    {
        public float3 Origin;
        public float3 Direction;
        public float  Speed;
        public float  Damage;
        public float  MaxDistance;

        public float  CritChance;
        public float  CritMultiplier;

        public float  PierceChance;
        public int    PierceMaxTargets;

        public int    HasValue;
    }

    public struct RetargetAssist : IComponentData
    {
        public float3 LastPos;
        public float  LastDistSq;
        public float  NoProgressTime;
    }
}