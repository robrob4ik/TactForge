// File: Assets/PROJECT/Scripts/ECS/Spatial/SpatialHashSearch.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.ECS
{
    public static class SpatialHashSearch
    {
        public static Entity GetClosest(
            float3 position,
            float maxDistance,
            FixedList128Bytes<byte> acceptedFactions,
            ref ComponentLookup<LocalTransform> transforms,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> targets)
        {
            var grid = SpatialHashBuildSystem.Grid;
            if (!grid.IsCreated)
                return Entity.Null;

            float cellSize = SpatialHashBuildSystem.CellSize;
            int   range    = (int)math.ceil(maxDistance / cellSize);
            int3  baseCell = (int3)math.floor(new float3(position.x, 0f, position.z) / cellSize);

            float bestDistSq = maxDistance * maxDistance;
            Entity best = Entity.Null;

            for (int dz = -range; dz <= range; dz++)
            for (int dx = -range; dx <= range; dx++)
            {
                int3 cell = baseCell + new int3(dx, 0, dz);
                int  key  = (int)math.hash(cell);

                if (!grid.TryGetFirstValue(key, out var e, out var it))
                    continue;

                do
                {
                    if (!transforms.HasComponent(e)) continue;
                    if (!targets.HasComponent(e))    continue;

                    var pos = transforms[e].Position;
                    float sqr = math.distancesq(pos, position);
                    if (sqr >= bestDistSq) continue;

                    if (!acceptedFactions.Contains(targets[e].Faction)) continue;

                    bestDistSq = sqr;
                    best = e;
                }
                while (grid.TryGetNextValue(out e, ref it));
            }

            return best;
        }

        public static void CollectInSphere(
            float3 position,
            float radius,
            FixedList128Bytes<byte> acceptedFactions,
            NativeList<Entity> results,
            ref ComponentLookup<LocalTransform> transforms,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factions)
        {
            var grid = SpatialHashBuildSystem.Grid;
            if (!grid.IsCreated)
                return;

            float cellSize = SpatialHashBuildSystem.CellSize;
            int   range    = (int)math.ceil(radius / cellSize);
            int3  baseCell = (int3)math.floor(new float3(position.x, 0f, position.z) / cellSize);
            float radiusSq = radius * radius;

            for (int dz = -range; dz <= range; dz++)
            for (int dx = -range; dx <= range; dx++)
            {
                int3 cell = baseCell + new int3(dx, 0, dz);
                int  key  = (int)math.hash(cell);

                if (!grid.TryGetFirstValue(key, out var e, out var it))
                    continue;

                do
                {
                    if (!transforms.HasComponent(e)) continue;
                    if (!factions.HasComponent(e))   continue;

                    var pos = transforms[e].Position;
                    if (math.distancesq(pos, position) > radiusSq) continue;

                    if (!acceptedFactions.Contains(factions[e].Faction)) continue;

                    results.Add(e);
                }
                while (grid.TryGetNextValue(out e, ref it));
            }
        }
    }
}
