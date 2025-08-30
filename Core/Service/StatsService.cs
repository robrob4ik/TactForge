// File: OneBitRob/Services/StatsService.cs

using OneBitRob.AI;
using OneBitRob.ECS;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob
{
    public static class StatsService
    {
        public static void AddModifier(Entity e, in StatModifierData data)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            var em = world.EntityManager;

            var buf = em.HasBuffer<StatModifier>(e)
                ? em.GetBuffer<StatModifier>(e)
                : em.AddBuffer<StatModifier>(e);

            buf.Add(new StatModifier
            {
                Kind = data.Kind,
                Op   = data.Op,
                Value= data.Value
            });

            if (!em.HasComponent<UnitRuntimeStats>(e))
                em.AddComponentData(e, UnitRuntimeStats.Defaults);

            if (!em.HasComponent<StatsDirtyTag>(e))
                em.AddComponent<StatsDirtyTag>(e);
        }

        public static void AddModifiers(Entity e, StatModSetDefinition set)
        {
            if (!set) return;
            foreach (var entry in set.entries)
                AddModifier(e, entry);
        }

        public static void AddModifier(UnitBrain brain, in StatModifierData data)
        {
            if (!brain) return;
            var e = brain.GetEntity();
            if (e == Entity.Null) return;
            AddModifier(e, data);
        }

        public static void AddModifiers(UnitBrain brain, StatModSetDefinition set)
        {
            if (!brain) return;
            var e = brain.GetEntity();
            if (e == Entity.Null) return;
            AddModifiers(e, set);
        }
    }
}