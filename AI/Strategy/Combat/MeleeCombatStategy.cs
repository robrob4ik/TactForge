using OneBitRob.ECS;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    public class MeleeCombatStrategy : ICombatStrategy
    {
        public void Attack(UnitBrain brain, Transform target)
        {
            // Route all melee attacks through ECS so we have one throttle path.
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
