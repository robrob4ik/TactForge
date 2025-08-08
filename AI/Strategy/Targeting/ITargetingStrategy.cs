using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


namespace OneBitRob.AI
{
    public interface ITargetingStrategy
    {
        GameObject GetTarget(
            float3 position,
            float maxDistance,
            in FixedList128Bytes<byte> acceptedFactions,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factionLookup
        );
    }
}