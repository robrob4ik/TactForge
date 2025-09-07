// ECS/HybridSync/HybridSyncGroups.cs

using OneBitRob.AI;
using Opsive.BehaviorDesigner.Runtime.Groups;
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
    
    
    // Parent AI group (stays where it is in the Simulation pipeline)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BehaviorTreeSystemGroup))]
    [UpdateAfter(typeof(SpatialHashBuildSystem))]
    public partial class AITaskSystemGroup : ComponentSystemGroup {}

    // Phase 1: Planning / selection
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class AIPlanPhaseGroup : ComponentSystemGroup {}

    // Phase 2: Casting / windup / cooldown application
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AIPlanPhaseGroup))]
    public partial class AICastPhaseGroup : ComponentSystemGroup {}

    // Phase 3: After‑effects / resolutions that depend on casting
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(AICastPhaseGroup))]
    public partial class AIResolvePhaseGroup : ComponentSystemGroup {}
}