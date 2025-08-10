using OneBitRob.AI;
using Unity.Entities;
using OneBitRob.ECS;

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

                // Spell (if the component exists)
                if (em.HasComponent<SpellState>(e))
                {
                    var ss = em.GetComponentData<SpellState>(e);
                    ss.CanCast = (byte)(brain.CanCastSpell()    ? 1 : 0);
                    ss.Ready   = (byte)(brain.ReadyToCastSpell()? 1 : 0);
                    em.SetComponentData(e, ss);
                }
            }
        }
    }
}