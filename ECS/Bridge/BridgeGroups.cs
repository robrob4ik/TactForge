// ECS/HybridSync/HybridSyncGroups.cs

using OneBitRob.AI;
using Unity.Entities;

namespace OneBitRob.ECS
{
    /// Runs before spatial hash & any ECS readers so ECS always sees fresh Mono state.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SpatialHashBuildSystem))]
    public sealed partial class MonoToEcsSyncGroup : ComponentSystemGroup {}

    /// Runs after AI/task systems so Mono receives final ECS intents this frame.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AITaskSystemGroup))]
    public sealed partial class EcsToMonoBridgeGroup : ComponentSystemGroup {}
}