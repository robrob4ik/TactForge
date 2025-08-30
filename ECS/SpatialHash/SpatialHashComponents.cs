using Unity.Entities;

namespace OneBitRob.ECS
{
    public class SpatialHashComponents
    {
        public struct SpatialHashTarget : IComponentData
        {
            public byte Faction;
        }

        public struct SpatialHashSettings : IComponentData
        {
            public float CellSize;    // World‑space size of one cubic cell.
        }
        
    }
}