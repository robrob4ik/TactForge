// File: OneBitRob/Core/EcbExtensions.cs
using Unity.Entities;

namespace OneBitRob.Core
{
    public static class EcbExtensions
    {
        /// <summary>Set a component if present, else add it (EntityManager).</summary>
        public static void SetOrAdd<T>(this EntityManager em, Entity e, T value)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(e)) em.SetComponentData(e, value);
            else                       em.AddComponentData(e, value);
        }

        /// <summary>Add component if missing (EntityManager).</summary>
        public static void AddIfMissing<T>(this EntityManager em, Entity e)
            where T : unmanaged, IComponentData
        {
            if (!em.HasComponent<T>(e)) em.AddComponent<T>(e);
        }

        /// <summary>Set or add a component via ECB (non‑enableable).</summary>
        public static void SetOrAdd<T>(this EntityCommandBuffer ecb, EntityManager em, Entity e, T value)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(e)) ecb.SetComponent(e, value);
            else                       ecb.AddComponent(e, value);
        }

        /// <summary>Set or add, then enable an enableable component (ECB).</summary>
        public static void SetOrAddAndEnable<T>(this EntityCommandBuffer ecb, EntityManager em, Entity e, T value)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (!em.HasComponent<T>(e)) ecb.AddComponent<T>(e);
            ecb.SetComponent(e, value);
            ecb.SetComponentEnabled<T>(e, true);
        }

        /// <summary>Enable an enableable component (ECB).</summary>
        public static void Enable<T>(this EntityCommandBuffer ecb, Entity e)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            ecb.SetComponentEnabled<T>(e, true);
        }

        /// <summary>Disable an enableable component (ECB).</summary>
        public static void Disable<T>(this EntityCommandBuffer ecb, Entity e)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            ecb.SetComponentEnabled<T>(e, false);
        }
    }
}
