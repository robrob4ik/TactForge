using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct HealthMirror : IComponentData
    {
        public float Current;
        public float Max;
    }
    
    /// <summary>Lifecycle gate used to orchestrate death animation/FX before teardown.</summary>
    public struct DeathSequence : IComponentData
    {
        public byte  Started;
        public float StartTime;
        public float Duration;
        public float HardTimeout;
    }
    public struct DeathSequenceOverride : IComponentData
    {
        public float Duration;
        public float HardTimeout;
    }
}