// FILE: OneBitRob/AI/SpellTargeting/LowestHealthAllyTargeting.cs
using OneBitRob.Constants;
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public struct LowestHealthAllyTargeting : ISpellTargetingStrategy
    {
        public Entity GetTarget(
            Entity self,
            in SpellConfig config,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factLookup,
            ref ComponentLookup<HealthMirror> healthLookup)
        {
            byte selfFaction = factLookup.HasComponent(self) ? factLookup[self].Faction : GameConstants.ALLY_FACTION;

            var wanted = new FixedList128Bytes<byte>();
            wanted.Add(selfFaction);

            using var ents = new NativeList<Entity>(Allocator.Temp);
            SpatialHashSearch.CollectInSphere(
                posLookup.HasComponent(self) ? posLookup[self].Position : float3.zero,
                config.Range,
                wanted,
                ents,
                ref posLookup,
                ref factLookup);

            Entity best = Entity.Null;
            float lowestPct = 2f;

            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                if (!healthLookup.HasComponent(e)) continue;

                var hm = healthLookup[e];
                if (hm.Max <= 0 || hm.Current >= hm.Max) continue;

                float pct = (float)hm.Current / hm.Max;
                if (pct < lowestPct)
                {
                    lowestPct = pct;
                    best = e;
                }
            }

            return best;
        }

        public bool TryGetAOETargetPoint(
            Entity _,
            in SpellConfig __,
            ref ComponentLookup<LocalTransform> ___,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> ____,
            out float3 point)
        {
            point = default;
            return false;
        }
    }
}
