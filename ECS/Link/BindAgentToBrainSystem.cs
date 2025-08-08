using OneBitRob.AI;
using Unity.Collections;
using Unity.Entities;
using ProjectDawn.Navigation.Hybrid;
using static OneBitRob.ECS.Link.LinkComponents;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class BindAgentToBrainSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Filter by the *data* tag (IComponentData), Burst‑friendly
        foreach (var (brainTag, entity) in
                 SystemAPI.Query<RefRO<UnitBrainTag>>().WithEntityAccess())
        {
            // Already processed?
            if (EntityManager.HasComponent<AgentEntityRef>(entity))
                continue;

            // Get the MonoBehaviour that owns this BT entity
            var brainRef   = EntityManager.GetSharedComponentManaged<UnitBrainRef>(entity);
            var brainMono  = brainRef.Value;
            if (brainMono == null)
                continue;

            // Find the AgentAuthoring that drives movement
            var agent = brainMono.GetComponent<AgentAuthoring>();
            if (agent == null)
                continue;

            Entity navEntity = agent.GetOrCreateEntity();
            if (navEntity == Entity.Null)
                continue;

            ecb.AddComponent(entity,   new AgentEntityRef { Value = navEntity });
            ecb.AddComponent(navEntity,new BrainEntityRef { Value = entity   });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}