// Assets/_Game/AI/Movement/NavStoppingDistanceApplySystem.cs

using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// TODO Better name
namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AITaskSystemGroup))]
    public partial struct NavStoppingDistanceApplySystem : ISystem
    {
        private EntityQuery _q;

        public void OnCreate(ref SystemState state)
        {
            _q = SystemAPI.QueryBuilder()
                .WithAllRW<AgentLocomotion>()
                .WithAllRW<DesiredStoppingDistance>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var ents = _q.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var stop = em.GetComponentData<DesiredStoppingDistance>(e);
                if (stop.HasValue == 0) continue;

                var loco = em.GetComponentData<AgentLocomotion>(e);
                float v = math.max(0f, stop.Value);
                if (math.abs(loco.StoppingDistance - v) > 0.0001f)
                {
                    loco.StoppingDistance = v;
                    em.SetComponentData(e, loco);
                }
                em.SetComponentData(e, default(DesiredStoppingDistance)); // consume
            }
        }
    }
}