// ECS/HybridSync/MonoToEcs/Brain_MonoToEcs_HealthMirrorSystem.cs
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.ECS
{
    /// Mirrors UnitBrain.Health -> HealthMirror (ECS-readable health).
    [UpdateInGroup(typeof(MonoToEcsSyncGroup))]
    public partial struct Brain_MonoToEcs_HealthMirrorSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var ents = SystemAPI.QueryBuilder()
                .WithAll<AgentTag>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (brain?.Health == null) continue;

                if (!em.HasComponent<HealthMirror>(e))
                {
                    ecb.AddComponent(e, new HealthMirror
                    {
                        Current = brain.Health.CurrentHealth,
                        Max     = brain.Health.MaximumHealth
                    });
                }
                else
                {
                    var hm = em.GetComponentData<HealthMirror>(e);
                    hm.Current = brain.Health.CurrentHealth;
                    hm.Max     = brain.Health.MaximumHealth;
                    em.SetComponentData(e, hm);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
            ents.Dispose();
        }
    }
}