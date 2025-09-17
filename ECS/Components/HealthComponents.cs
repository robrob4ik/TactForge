using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct HealthMirror : IComponentData
    {
        public float Current;
        public float Max;
    }
}