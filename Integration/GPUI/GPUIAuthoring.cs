using GPUInstancerPro.PrefabModule;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS.GPUI
{
    public class GPUIAuthoring : EntityBehaviour
    {
        [SerializeField] private GPUIPrefabManager prefabManager;
        
        void Awake()
        {
            var em      = World.DefaultGameObjectInjectionWorld.EntityManager;
            var entity  = em.CreateEntity();                             
            em.AddComponentObject(entity, new GPUIManagerRef             
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