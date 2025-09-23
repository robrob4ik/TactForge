// Assets/_Game/AI/Objectives/PlayerBaseAuthoring.cs

using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    public sealed class PlayerBaseAuthoring : EntityBehaviour
    {
        public Transform centerOverride;
        [Min(0f)] public float holdRadius = 2.0f;

        void Awake()
        {
            m_Entity = GetOrCreateEntity();
            var em   = World.DefaultGameObjectInjectionWorld.EntityManager;

            em.AddComponentObject(m_Entity, transform);

            var pos = (centerOverride ? centerOverride.position : transform.position);
            em.AddComponentData(m_Entity, new PlayerBase
            {
                Position   = pos,
                HoldRadius = math.max(0f, holdRadius)
            });
        }
    }

    public struct PlayerBase : IComponentData
    {
        public float3 Position;
        public float  HoldRadius;
    }
}