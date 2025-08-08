using System.Collections.Generic;
using OneBitRob.ECS;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    public interface ISpellTargetingStrategy
    {
        GameObject GetTarget(
            UnitBrain brain,
            SpellDefinition spell,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factionLookup
        );

        List<GameObject> GetTargets(
            UnitBrain brain,
            SpellDefinition spell,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factionLookup
        );

        Vector3? GetAOETargetPoint(
            UnitBrain brain,
            SpellDefinition spell,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factionLookup
        );
    }
}