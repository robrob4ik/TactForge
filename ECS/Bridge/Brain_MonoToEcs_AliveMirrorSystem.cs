// ECS/HybridSync/MonoToEcs/Brain_MonoToEcs_AliveMirrorSystem.cs
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.ECS
{
    /// Mirrors CombatSubsystem.IsAlive -> Alive and tags for cleanup when dead.
    [UpdateInGroup(typeof(MonoToEcsSyncGroup))]
    public partial struct Brain_MonoToEcs_AliveMirrorSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var ents = SystemAPI.QueryBuilder()
                .WithAll<AgentTag, Alive>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (brain == null) continue;

                // Alive flag
                var alive = em.GetComponentData<Alive>(e);
                bool monoAlive = brain.CombatSubsystem != null && brain.CombatSubsystem.IsAlive;
                alive.Value = (byte)(monoAlive ? 1 : 0);
                em.SetComponentData(e, alive);

                // If dead, tag for cleanup once (structural change deferred)
                if (!monoAlive && !em.HasComponent<DestroyEntityTag>(e))
                    ecb.AddComponent<DestroyEntityTag>(e);
            }

            ecb.Playback(em);
            ecb.Dispose();
            ents.Dispose();
        }
    }
}