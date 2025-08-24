// Runtime/ECS/Spell/SpellMovementLockSystem.cs

using OneBitRob.AI;
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(OneBitRob.AI.SpellDecisionSystem))]
    [UpdateBefore(typeof(OneBitRob.Bridge.MonoBridgeSystem))]
    public partial struct SpellMovementLockSystem : ISystem
    {
        private EntityQuery _q;

        public void OnCreate(ref SystemState state)
        {
            _q = state.GetEntityQuery(
                ComponentType.ReadOnly<SpellWindup>(),
                ComponentType.ReadWrite<MovementLock>());
            // We also want entities that *don’t* have MovementLock yet:
            state.RequireForUpdate(state.GetEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<SpellWindup>() }
            }));
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ents = SystemAPI.QueryBuilder().WithAll<SpellWindup>().Build().ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var w = em.GetComponentData<SpellWindup>(e);

                var ml = em.HasComponent<MovementLock>(e)
                    ? em.GetComponentData<MovementLock>(e)
                    : new MovementLock { Flags = MovementLockFlags.None };

                if (w.Active != 0)
                    ml.Flags |= MovementLockFlags.Casting;
                else
                    ml.Flags &= ~MovementLockFlags.Casting;

                if (em.HasComponent<MovementLock>(e)) ecb.SetComponent(e, ml);
                else                                   ecb.AddComponent(e, ml);
            }

            ecb.Playback(em);
            ecb.Dispose();
            ents.Dispose();
        }
    }
}