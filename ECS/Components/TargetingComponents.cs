// Assets/PROJECT/Scripts/Runtime/ECS/Core/Components/TargetingComponents.cs
using Unity.Entities;
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    public struct Target : IComponentData { public Entity Value; }

    public struct InAttackRange : IComponentData
    {
        public byte  Value;
        public float DistanceSq;
    }
    
    public struct RetargetAssist : IComponentData
    {
        public float3 LastPos;
        public float  LastDistSq;
        public float  NoProgressTime;
    }
    
    public struct RetargetCooldown : IComponentData { public double NextTime; }

}