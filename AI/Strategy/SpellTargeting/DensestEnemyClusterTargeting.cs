using System.Collections.Generic;
using OneBitRob.Constants;
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    public struct DensestEnemyClusterTargeting : ISpellTargetingStrategy
    {
        public GameObject GetTarget(UnitBrain _, SpellDefinition __,
            ref ComponentLookup<LocalTransform> ___,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> ____) =>
            null;

        public List<GameObject> GetTargets(UnitBrain _, SpellDefinition __,
            ref ComponentLookup<LocalTransform> ___,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> ____) =>
            null;

        public Vector3? GetAOETargetPoint(UnitBrain brain, SpellDefinition spell,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factLookup)
        {
            var wanted = new FixedList128Bytes<byte>();
            wanted.Add(
                brain.UnitDefinition.isEnemy
                    ? GameConstants.ENEMY_FACTION
                    : GameConstants.ALLY_FACTION
            );

            using var ents = new NativeList<Entity>(Allocator.Temp);
            SpatialHashSearch.CollectInSphere(
                brain.transform.position,
                spell.Range,
                wanted,
                ents,
                ref posLookup,
                ref factLookup
            );

            if (ents.Length == 0) return null;

            float bestCount = 0;
            float3 bestCenter = float3.zero;

            for (int i = 0; i < ents.Length; i++)
            {
                var centerPos = posLookup[ents[i]].Position;
                int count = 0;

                for (int j = 0; j < ents.Length; j++)
                {
                    if (math.distancesq(centerPos, posLookup[ents[j]].Position)
                        <= spell.AreaRadius * spell.AreaRadius)
                        count++;
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    bestCenter = centerPos;
                }
            }

            return bestCount > 0 ? (Vector3)bestCenter : null;
        }
    }
}