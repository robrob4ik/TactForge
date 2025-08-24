// ECS/HybridSync/MonoToEcs/Brain_MonoToEcs_SpellStateMirrorSystem.cs
using Unity.Entities;

namespace OneBitRob.ECS
{
    /// Sets SpellState.CanCast/Ready from ECS spell components (no Mono reads).
    /// Keeps the convention from your original code but isolates it.
    [UpdateInGroup(typeof(MonoToEcsSyncGroup))]
    public partial struct Brain_MonoToEcs_SpellStateMirrorSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em  = state.EntityManager;
            float now = (float)SystemAPI.Time.ElapsedTime;

            var q = SystemAPI.QueryBuilder()
                .WithAll<SpellState>()
                .Build();

            var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e  = ents[i];
                var ss = em.GetComponentData<SpellState>(e);

                if (em.HasComponent<SpellConfig>(e) &&
                    em.HasComponent<SpellCooldown>(e) &&
                    em.HasComponent<SpellWindup>(e))
                {
                    var cd = em.GetComponentData<SpellCooldown>(e);
                    var w  = em.GetComponentData<SpellWindup>(e);
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
            ents.Dispose();
        }
    }
}