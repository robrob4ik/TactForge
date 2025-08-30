using GPUInstancerPro.PrefabModule;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS.GPUI
{
    public class GPUIBootstrap : MonoBehaviour
    {
        [SerializeField] private GPUIPrefabManager prefabManager;
        
        void Awake()
        {
            var em      = World.DefaultGameObjectInjectionWorld.EntityManager;
            var entity  = em.CreateEntity();                             // empty entity
            em.AddComponentObject(entity, new GPUIManagerRef             // <-- Add *managed* component
            {
                Value = prefabManager
            });
        }
    }

    public class GPUIManagerRef : IComponentData
    {
        public GPUIPrefabManager Value;
    }
}