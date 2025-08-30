using Unity.Entities;

namespace OneBitRob.ECS
{
    public static class EcbExtensions
    {
        public static void SetOrAdd<T>(this EntityCommandBuffer ecb, EntityManager em, Entity e, in T value)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(e)) ecb.SetComponent(e, value);
            else ecb.AddComponent(e, value);
        }
    }
}