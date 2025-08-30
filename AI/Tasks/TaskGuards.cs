using OneBitRob.ECS;
using Unity.Entities;

namespace OneBitRob.AI
{
    public static class TaskGuards
    {
        public static bool IsBlockedByCastOrAttack(EntityManager em, Entity e)
        {
            if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0) return true;
            if (em.HasComponent<AttackWindup>(e) && em.GetComponentData<AttackWindup>(e).Active != 0) return true;

            if (em.HasComponent<MovementLock>(e))
            {
                var f = em.GetComponentData<MovementLock>(e).Flags;
                if ((f & (MovementLockFlags.Casting | MovementLockFlags.Attacking)) != 0) return true;
            }
            return false;
        }
    }
}