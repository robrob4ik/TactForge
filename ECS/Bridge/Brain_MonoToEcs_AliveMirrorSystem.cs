// FILE: Assets/PROJECT/Scripts/Runtime/ECS/Brain/Brain_MonoToEcs_AliveMirrorSystem.cs

using OneBitRob.AI;
using Unity.Collections;
using Unity.Entities;
using OneBitRob.FX; // <-- add

namespace OneBitRob.ECS
{
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
                var brain = UnitBrainRegistry.Get(e);
                if (brain == null) continue;

                // Alive flag
                var alive = em.GetComponentData<Alive>(e);
                bool monoAlive = brain.CombatSubsystem != null && brain.CombatSubsystem.IsAlive;
                alive.Value = (byte)(monoAlive ? 1 : 0);
                em.SetComponentData(e, alive);

                // If dead, tag for cleanup once and trigger death feedback once
                if (!monoAlive && !em.HasComponent<DestroyEntityTag>(e))
                {
                    ecb.AddComponent<DestroyEntityTag>(e);

                    var ud = brain.UnitDefinition;
                    if (ud != null && ud.deathFeedback != null)
                    {
                        FeedbackService.TryPlay(ud.deathFeedback, null, brain.transform.position);
                    }
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
            ents.Dispose();
        }
    }
}