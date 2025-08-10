using OneBitRob.ECS;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    public class RangedCombatStrategy : ICombatStrategy
    {
        public void Attack(UnitBrain brain, Transform target)
        {
            // Same as melee: write request, ECS decides if/when to actually shoot.
            if (brain == null || target == null) return;

            var e = brain.GetEntity();
            if (e == Entity.Null) return;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var tgtEnt = UnitBrainRegistry.GetEntity(target.gameObject);
            if (tgtEnt == Entity.Null) return;

            var req = new AttackRequest { Target = tgtEnt, HasValue = 1 };
            if (em.HasComponent<AttackRequest>(e)) em.SetComponentData(e, req);
            else em.AddComponentData(e, req);
        }
    }
}
