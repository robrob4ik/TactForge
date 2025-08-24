// Assets/PROJECT/Scripts/Runtime/ECS/Core/Components/TargetingComponents.cs
using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct Target : IComponentData { public Entity Value; }

    public struct InAttackRange : IComponentData
    {
        public byte  Value;
        public float DistanceSq;
    }
}