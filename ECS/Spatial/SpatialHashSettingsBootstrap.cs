using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    [DisallowMultipleComponent]
    public class SpatialHashSettingsBootstrap : EntityBehaviour
    {
        [SerializeField]
        internal float CellSize = 3f;

        void Awake()
        {
            m_Entity = GetOrCreateEntity();
            var world = World.DefaultGameObjectInjectionWorld;
            var manager = world.EntityManager;


            manager.AddComponentObject(m_Entity, transform);
            manager.AddComponentData(m_Entity, new SpatialHashComponents.SpatialHashSettings { CellSize = CellSize });
        }
    }

    class Baker : Baker<SpatialHashSettingsBootstrap>
    {
        public override void Bake(SpatialHashSettingsBootstrap bootstrap)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new SpatialHashComponents.SpatialHashSettings { CellSize = bootstrap.CellSize });
        }
    }
}