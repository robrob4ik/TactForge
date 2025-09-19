using OneBitRob.ECS;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public interface ISpellTargetingStrategy
    {
        Entity GetTarget(
            Entity self,
            in SpellConfig config,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashTarget> factionLookup,
            ref ComponentLookup<HealthMirror> healthLookup
        );

        bool TryGetAOETargetPoint(
            Entity self,
            in SpellConfig config,
            ref ComponentLookup<LocalTransform> posLookup,
            ref ComponentLookup<SpatialHashTarget> factionLookup,
            out float3 point
        );
    }
}