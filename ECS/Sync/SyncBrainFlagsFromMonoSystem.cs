// CHANGED: auto-add HealthMirror if missing so healers can find allies reliably.

using OneBitRob.AI;
using Unity.Entities;

namespace OneBitRob.ECS.Sync
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AITaskSystemGroup))]
    public partial class SyncBrainFlagsFromMonoSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            foreach (var e in SystemAPI.QueryBuilder().WithAll<AgentTag, Alive>().Build().ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (brain == null) continue;

                // Alive flag
                var alive = em.GetComponentData<Alive>(e);
                alive.Value = (byte)(brain.CombatSubsystem != null && brain.CombatSubsystem.IsAlive ? 1 : 0);
                em.SetComponentData(e, alive);

                // NEW: Ensure HealthMirror exists if we can mirror from Mono
                if (brain.Health != null && !em.HasComponent<HealthMirror>(e))
                {
                    em.AddComponentData(e, new HealthMirror
                    {
                        Current = brain.Health.CurrentHealth,
                        Max     = brain.Health.MaximumHealth
                    });
                }

                // Mirror SpellState.Ready (unchanged)
                if (em.HasComponent<SpellState>(e))
                {
                    var ss = em.GetComponentData<SpellState>(e);

                    if (em.HasComponent<SpellConfig>(e) && em.HasComponent<SpellCooldown>(e) && em.HasComponent<SpellWindup>(e))
                    {
                        var cd = em.GetComponentData<SpellCooldown>(e);
                        var w  = em.GetComponentData<SpellWindup>(e);
                        float now = (float)SystemAPI.Time.ElapsedTime;

                        ss.CanCast = 1;
                        ss.Ready   = (byte)((w.Active == 0 && now >= cd.NextTime) ? 1 : 0);
                    }
                    else
                    {
                        ss.CanCast = 0;
                        ss.Ready = 0;
                    }

                    em.SetComponentData(e, ss);
                }

                // Health mirror update
                if (em.HasComponent<HealthMirror>(e) && brain.Health != null)
                {
                    var hm = em.GetComponentData<HealthMirror>(e);
                    hm.Current = brain.Health.CurrentHealth;
                    hm.Max     = brain.Health.MaximumHealth;
                    em.SetComponentData(e, hm);
                }
            }
        }
    }
}
