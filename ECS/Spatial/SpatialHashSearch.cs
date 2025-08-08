using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.ECS
{
    public static class SpatialHashSearch
    {
        public static Entity GetClosest
        (
            float3 position,
            float maxDistance,
            FixedList128Bytes<byte> acceptedFactions,
            ref ComponentLookup<LocalTransform> transforms,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> targets
        )
        {
            if (!SpatialHashBuildSystem.Grid.IsCreated || SpatialHashBuildSystem.Grid.Count() == 0) return Entity.Null;

            float cellSize = SpatialHashBuildSystem.CellSize;
            int range = (int)math.ceil(maxDistance / cellSize);
            int3 baseCell = (int3)math.floor(position / cellSize);

            float bestDistSq = maxDistance * maxDistance;
            Entity best = Entity.Null;

            var grid = SpatialHashBuildSystem.Grid;

            for (int z = -range; z <= range; z++)
            for (int y = -range; y <= range; y++)
            for (int x = -range; x <= range; x++)
            {
                int3 cell = baseCell + new int3(x, y, z);
                int key = (int)math.hash(cell);

                if (!grid.TryGetFirstValue(key, out var e, out var it)) continue;

                do
                {
                    if (!transforms.HasComponent(e)) continue;

                    var pos = transforms[e].Position;
                    float sqr = math.distancesq(pos, position);
                    if (sqr < bestDistSq)
                    {
                        if (!targets.HasComponent(e)) continue;
                        byte faction = targets[e].Faction;
                        if (acceptedFactions.Contains(faction))
                        {
                            bestDistSq = sqr;
                            best = e;
                        }
                    }
                } while (grid.TryGetNextValue(out e, ref it));
            }

            return best;
        }
        
        public static void CollectInSphere(
            float3                                  position,
            float                                   radius,
            FixedList128Bytes<byte>              acceptedFactions,
            NativeList<Entity>                      results,
            ref ComponentLookup<LocalTransform>     transforms,
            ref ComponentLookup<SpatialHashComponents.SpatialHashTarget> factions)
        {
            if (!SpatialHashBuildSystem.Grid.IsCreated || SpatialHashBuildSystem.Grid.Count() == 0)
                return;

            float cellSize = SpatialHashBuildSystem.CellSize;
            int   range    = (int)math.ceil(radius / cellSize);
            int3  baseCell = (int3)math.floor(position / cellSize);

            float radiusSq = radius * radius;
            var   grid     = SpatialHashBuildSystem.Grid;

            for (int z = -range; z <= range; z++)
            for (int y = -range; y <= range; y++)
            for (int x = -range; x <= range; x++)
            {
                int3 cell = baseCell + new int3(x, y, z);
                int  key  = (int)math.hash(cell);

                if (!grid.TryGetFirstValue(key, out var e, out var it)) continue;

                do
                {
                    if (!transforms.HasComponent(e))                           continue;
                    if (!factions.HasComponent(e))                             continue;

                    var pos = transforms[e].Position;
                    if (math.distancesq(pos, position) > radiusSq)            continue;

                    byte faction = factions[e].Faction;
                    if (!acceptedFactions.Contains(faction))                   continue;

                    results.Add(e);
                }
                while (grid.TryGetNextValue(out e, ref it));
            }
        }
    }
}