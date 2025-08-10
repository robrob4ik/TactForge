// FILE: OneBitRob/AI/Combat/EcsAttackDispatcher.cs
using OneBitRob.ECS;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    /// <summary>
    /// Minimal helper to write AttackRequest to ECS (KISS instead of a "strategy" layer).
    /// </summary>
    public static class EcsAttackDispatcher
    {
        public static void Request(UnitBrain brain, Transform target)
        {
            if (brain == null || target == null) return;

            var e = brain.GetEntity();
            if (e == Entity.Null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            var tgtEnt = UnitBrainRegistry.GetEntity(target.gameObject);
            if (tgtEnt == Entity.Null) return;

            var req = new AttackRequest { Target = tgtEnt, HasValue = 1 };
            if (em.HasComponent<AttackRequest>(e)) em.SetComponentData(e, req);
            else em.AddComponentData(e, req);
        }
    }
}