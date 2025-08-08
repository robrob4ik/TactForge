using System.Collections.Generic;
using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    public struct ClosestEnemyTargeting : ITargetingStrategy
    {
        public GameObject GetTarget(
            float3                                position,
            float                                 maxDistance,
            in FixedList128Bytes<byte>            acceptedFactions,
            ref ComponentLookup<LocalTransform>   posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factionLookup)
        {
            Entity e = SpatialHashSearch.GetClosest(
                position, maxDistance, acceptedFactions,
                ref posLookup, ref factionLookup);

            return e != Entity.Null ? UnitBrainRegistry.Get(e)?.gameObject : null;
        }
    }
}