using OneBitRob.Constants;
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public struct ClosestEnemySpellTargeting : ISpellTargetingStrategy
    {
        public Entity GetTarget(
            Entity self,
            in SpellConfig config,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashTarget> factLookup,
            ref ComponentLookup<HealthMirror> _)
        {
            byte selfFaction = factLookup.HasComponent(self) ? factLookup[self].Faction : GameConstants.ALLY_FACTION;
            byte enemyFaction = (selfFaction == GameConstants.ENEMY_FACTION) ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION;

            var wanted = new FixedList128Bytes<byte>();
            wanted.Add(enemyFaction);

            return SpatialHashSearch.GetClosest(
                posLookup.HasComponent(self) ? posLookup[self].Position : float3.zero,
                config.Range,
                wanted,
                ref posLookup,
                ref factLookup);
        }

        public bool TryGetAOETargetPoint(
            Entity _,
            in SpellConfig __,
            ref ComponentLookup<LocalTransform> ___,
            ref ComponentLookup<SpatialHashTarget> ____,
            out float3 point)
        {
            point = default;
            return false;
        }
    }
}