using System.Collections.Generic;
using OneBitRob.Constants;
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    public struct ClosestEnemySpellTargeting : ISpellTargetingStrategy
    {
        public GameObject GetTarget(UnitBrain brain, SpellDefinition spell,
            ref ComponentLookup<LocalTransform>            posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factLookup)
        {
            var wanted = new FixedList128Bytes<byte>();
            wanted.Add(brain.UnitDefinition.isEnemy
                ? GameConstants.ENEMY_FACTION
                : GameConstants.ALLY_FACTION);

            Entity e = SpatialHashSearch.GetClosest(
                brain.transform.position, spell.Range, wanted,
                ref posLookup, ref factLookup);

            return e != Entity.Null ? UnitBrainRegistry.Get(e)?.gameObject : null;
        }

        public List<GameObject> GetTargets(UnitBrain _, SpellDefinition __,
            ref ComponentLookup<LocalTransform> ___,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> ____)
            => null;

        public Vector3? GetAOETargetPoint(UnitBrain _, SpellDefinition __,
            ref ComponentLookup<LocalTransform> ___,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> ____)
            => null;
    }
}