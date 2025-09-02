using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct SpatialHashTarget : IComponentData
    {
        public byte Faction;
    }

    public struct SpatialHashSettings : IComponentData
    {
        public float CellSize; 
    }
}