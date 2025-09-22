
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.ECS
{
    public enum SpawnerType 
    { Ally = 0, Enemy = 1 }
    
    [DisallowMultipleComponent]
    public class SpawnerMarkerAuthoring : EntityBehaviour
    {
        [SerializeField]
        internal SpawnerType Type;
        
        void Awake()
        {
            m_Entity = GetOrCreateEntity();
            var world = World.DefaultGameObjectInjectionWorld;
            var manager = world.EntityManager;
       
            manager.AddComponentData(m_Entity, new LocalTransform
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = 1,
            });

            manager.AddComponentObject(m_Entity, transform);
            manager.AddComponentData(m_Entity, new SpawnerMarker { Type = Type });
        }
        
    }
}