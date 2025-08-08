using OneBitRob.ECS.Link;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)] // after Nav update
public partial struct SyncBrainTransformSystem : ISystem
{
    ComponentLookup<LocalTransform> _navLookup;

    public void OnCreate(ref SystemState state)
    {
        _navLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        _navLookup.Update(ref state);

        var job = new SyncJob
        {
            NavLookup = _navLookup
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    partial struct SyncJob : IJobEntity
    {
        public ComponentLookup<LocalTransform> NavLookup;

        public void Execute(ref LocalTransform brainLt, in LinkComponents.AgentEntityRef agentRef)
        {
            if (!NavLookup.HasComponent(agentRef.Value)) return;
            brainLt = NavLookup[agentRef.Value];      // copy pos‑rot‑scale
        }
    }
}