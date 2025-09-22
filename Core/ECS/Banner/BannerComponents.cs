using Unity.Entities;
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    public struct BannerAssignment : IComponentData
    {
        public Entity Banner;
        public BannerStrategy Strategy;
        public float3 HomeOffset;
    }
}