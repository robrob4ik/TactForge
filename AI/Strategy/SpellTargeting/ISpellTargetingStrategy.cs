// FILE: OneBitRob/AI/ISpellTargetingStrategy.cs
using OneBitRob.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public interface ISpellTargetingStrategy
    {
        // Single-target selection. Return Entity.Null if not found.
        Entity GetTarget(
            Entity self,
            in SpellConfig config,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factionLookup,
            ref ComponentLookup<HealthMirror> healthLookup
        );

        // AoE selection. Returns true/point if found.
        bool TryGetAOETargetPoint(
            Entity self,
            in SpellConfig config,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factionLookup,
            out float3 point
        );
    }
}