using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.AI
{
    /// <summary>
    /// Sets/clears MovementLock.Attacking while:
    /// - AttackWindup.Active != 0 (ranged windup)
    /// - ActionLockUntil active (melee short lock)
    /// </summary>
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(WeaponAttackSystem))]
    public partial struct WeaponMovementLockSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em  = state.EntityManager;
            float now = (float)SystemAPI.Time.ElapsedTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Windup-based lock (ranged)
            foreach (var (w, e) in SystemAPI.Query<RefRO<OneBitRob.ECS.AttackWindup>>().WithEntityAccess())
            {
                var ml = em.HasComponent<OneBitRob.ECS.MovementLock>(e)
                    ? em.GetComponentData<OneBitRob.ECS.MovementLock>(e)
                    : new OneBitRob.ECS.MovementLock { Flags = OneBitRob.ECS.MovementLockFlags.None };

                if (w.ValueRO.Active != 0)
                    ml.Flags |= OneBitRob.ECS.MovementLockFlags.Attacking;
                else
                    ml.Flags &= ~OneBitRob.ECS.MovementLockFlags.Attacking;

                if (em.HasComponent<OneBitRob.ECS.MovementLock>(e)) ecb.SetComponent(e, ml);
                else                                               ecb.AddComponent(e, ml);
            }

            // Time-window lock (melee)
            foreach (var (win, e) in SystemAPI.Query<RefRO<OneBitRob.ECS.ActionLockUntil>>().WithEntityAccess())
            {
                bool active = now < win.ValueRO.Until;
                var ml = em.HasComponent<OneBitRob.ECS.MovementLock>(e)
                    ? em.GetComponentData<OneBitRob.ECS.MovementLock>(e)
                    : new OneBitRob.ECS.MovementLock { Flags = OneBitRob.ECS.MovementLockFlags.None };

                if (active) ml.Flags |= OneBitRob.ECS.MovementLockFlags.Attacking;
                else
                {
                    ml.Flags &= ~OneBitRob.ECS.MovementLockFlags.Attacking;
                    ecb.RemoveComponent<OneBitRob.ECS.ActionLockUntil>(e);
                }

                if (em.HasComponent<OneBitRob.ECS.MovementLock>(e)) ecb.SetComponent(e, ml);
                else                                               ecb.AddComponent(e, ml);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}
