// FILE: Assets/PROJECT/Scripts/ECS/Sync/SyncBrainFlagsFromMonoSystem.cs
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS.Sync
{
    // Mirror GameObject health/alive -> ECS and safely tag for destruction when Mono is dead.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(OneBitRob.ECS.SpatialHashBuildSystem))]
    public partial class SyncBrainFlagsFromMonoSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em  = EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var entities = SystemAPI.QueryBuilder()
                .WithAll<AgentTag, Alive>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var brain = OneBitRob.AI.UnitBrainRegistry.Get(e);
                if (brain == null) continue;

                // Alive mirror
                var alive = em.GetComponentData<Alive>(e);
                bool monoAlive = (brain.CombatSubsystem != null && brain.CombatSubsystem.IsAlive);
                alive.Value = (byte)(monoAlive ? 1 : 0);
                em.SetComponentData(e, alive);

                // Health mirror (Mono -> ECS)
                if (brain.Health != null)
                {
                    if (!em.HasComponent<HealthMirror>(e))
                    {
                        em.AddComponentData(e, new HealthMirror
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

                // Spell-state mirror (unchanged logic)
                if (em.HasComponent<SpellState>(e))
                {
                    var ss = em.GetComponentData<SpellState>(e);
                    if (em.HasComponent<SpellConfig>(e) && em.HasComponent<SpellCooldown>(e) && em.HasComponent<SpellWindup>(e))
                    {
                        var cd  = em.GetComponentData<SpellCooldown>(e);
                        var w   = em.GetComponentData<SpellWindup>(e);
                        float now = (float)SystemAPI.Time.ElapsedTime;
                        ss.CanCast = 1;
                        ss.Ready   = (byte)((w.Active == 0 && now >= cd.NextTime) ? 1 : 0);
                    }
                    else
                    {
                        ss.CanCast = 0;
                        ss.Ready   = 0;
                    }
                    em.SetComponentData(e, ss);
                }

                // NEW: tag for destroy when Mono is dead (ECB defers the structural change safely)
                if (!monoAlive && !em.HasComponent<DestroyEntityTag>(e))
                {
                    ecb.AddComponent<DestroyEntityTag>(e);
#if UNITY_EDITOR
                    Debug.Log($"[Sync] Tagging DestroyEntity: Entity({e.Index}:{e.Version}) '{(brain ? brain.name : "<no-go>")}'");
#endif
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
            entities.Dispose();
        }
    }
}
