// FILE: OneBitRob/ECS/Sync/SyncBrainFlagsFromMonoSystem.cs
using OneBitRob.AI;
using Unity.Entities;

namespace OneBitRob.ECS.Sync
{
    /// Mirrors Mono-side state into simple ECS flags so BT can stay pure ECS.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AITaskSystemGroup))]
    public partial class SyncBrainFlagsFromMonoSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            foreach (var e in SystemAPI.QueryBuilder().WithAll<AgentTag, Alive>().Build().ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                var brain = UnitBrainRegistry.Get(e);
                if (brain == null) continue;

                // Alive
                var alive = em.GetComponentData<Alive>(e);
                alive.Value = (byte)(brain.CombatSubsystem != null && brain.CombatSubsystem.IsAlive ? 1 : 0);
                em.SetComponentData(e, alive);

                // Spell flags derived from ECS (no Mono dependency)
                if (em.HasComponent<SpellState>(e))
                {
                    var ss = em.GetComponentData<SpellState>(e);

                    if (em.HasComponent<SpellConfig>(e) && em.HasComponent<SpellCooldown>(e) && em.HasComponent<SpellWindup>(e))
                    {
                        var cd = em.GetComponentData<SpellCooldown>(e);
                        var w  = em.GetComponentData<SpellWindup>(e);
                        float now = (float)SystemAPI.Time.ElapsedTime;

                        // CanCast: has config (always true here)
                        ss.CanCast = 1;
                        // Ready: not windup-active and not cooling down
                        ss.Ready = (byte)((w.Active == 0 && now >= cd.NextTime) ? 1 : 0);
                    }
                    else
                    {
                        ss.CanCast = 0;
                        ss.Ready = 0;
                    }

                    em.SetComponentData(e, ss);
                }

                // Health mirror (if present)
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
