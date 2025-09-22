using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AICastPhaseGroup))]
    [UpdateAfter(typeof(WeaponAttackSystem))]
    [UpdateAfter(typeof(SpellMovementLockSystem))]
    public partial struct WeaponMovementLockSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            float now = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Windup-based lock (ranged)
            foreach (var (w, e) in SystemAPI.Query<RefRO<AttackWindup>>().WithEntityAccess())
            {
                var ml = em.HasComponent<MovementLock>(e)
                    ? em.GetComponentData<MovementLock>(e)
                    : new MovementLock { Flags = MovementLockFlags.None };

                if (w.ValueRO.Active != 0)
                    ml.Flags |= MovementLockFlags.Attacking;
                else
                    ml.Flags &= ~MovementLockFlags.Attacking;

                if (em.HasComponent<MovementLock>(e))
                    ecb.SetComponent(e, ml);
                else
                    ecb.AddComponent(e, ml);
            }

            // Time-window lock (melee)
            foreach (var (win, e) in SystemAPI.Query<RefRO<ActionLockUntil>>().WithEntityAccess())
            {
                bool active = now < win.ValueRO.Until;
                var ml = em.HasComponent<MovementLock>(e)
                    ? em.GetComponentData<MovementLock>(e)
                    : new MovementLock { Flags = MovementLockFlags.None };

                if (active)
                    ml.Flags |= MovementLockFlags.Attacking;
                else
                {
                    ml.Flags &= ~MovementLockFlags.Attacking;
                    ecb.RemoveComponent<ActionLockUntil>(e);
                }

                if (em.HasComponent<MovementLock>(e))
                    ecb.SetComponent(e, ml);
                else
                    ecb.AddComponent(e, ml);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}