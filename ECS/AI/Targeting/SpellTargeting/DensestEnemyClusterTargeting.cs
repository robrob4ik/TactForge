using OneBitRob.Constants;
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public struct DensestEnemyClusterTargeting : ISpellTargetingStrategy
    {
        public Entity GetTarget(
            Entity _,
            in SpellConfig __,
            ref ComponentLookup<LocalTransform> ___,
            ref ComponentLookup<SpatialHashTarget> ____,
            ref ComponentLookup<HealthMirror> _____)
            => Entity.Null;

        public bool TryGetAOETargetPoint(
            Entity self,
            in SpellConfig config,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashTarget> factLookup,
            out float3 point)
        {
            point = default;

            byte selfFaction = factLookup.HasComponent(self) ? factLookup[self].Faction : GameConstants.ALLY_FACTION;
            byte enemyFaction = (selfFaction == GameConstants.ENEMY_FACTION)
                ? GameConstants.ALLY_FACTION
                : GameConstants.ENEMY_FACTION;

            var wanted = new FixedList128Bytes<byte>();
            wanted.Add(enemyFaction);

            using var ents = new NativeList<Entity>(Allocator.Temp);
            SpatialHashSearch.CollectInSphere(
                posLookup.HasComponent(self) ? posLookup[self].Position : float3.zero,
                config.Range,
                wanted,
                ents,
                ref posLookup,
                ref factLookup);

            if (ents.Length == 0) return false;

            float  bestCount  = 0;
            float3 bestCenter = float3.zero;
            float  radiusSq   = config.AreaRadius * config.AreaRadius;

            for (int i = 0; i < ents.Length; i++)
            {
                var centerPos = posLookup[ents[i]].Position;
                int count = 0;

                for (int j = 0; j < ents.Length; j++)
                {
                    if (math.distancesq(centerPos, posLookup[ents[j]].Position) <= radiusSq)
                        count++;
                }

                if (count > bestCount)
                {
                    bestCount  = count;
                    bestCenter = centerPos;
                }
            }

            if (bestCount > 0) { point = bestCenter; return true; }
            return false;
        }
    }
}
