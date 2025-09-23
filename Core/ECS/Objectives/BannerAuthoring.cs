using OneBitRob.Constants;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    public enum BannerStrategy : byte
    {
        Defend = 0,
        Poke = 1, 
        Aggro = 2
    }

    public sealed class BannerAuthoring : EntityBehaviour
    {
        [Tooltip("Ally = player side banners for now.")]
        public bool isEnemy = false;

        public BannerStrategy strategy = BannerStrategy.Defend;

        [Min(0f)]
        public float defendRadius = 4f;

        [Min(0f)]
        public float pokeAdvance = 8f;

        [Min(0f)]
        public float aggroRadius = 12f;
        
        void Awake()
        {
            m_Entity = GetOrCreateEntity();
            var world = World.DefaultGameObjectInjectionWorld;
            var manager = world.EntityManager;


            manager.AddComponentObject(m_Entity, transform);
            manager.AddComponentData(m_Entity, new Banner { Faction = (byte)(isEnemy ? GameConstants.ENEMY_FACTION : GameConstants.ALLY_FACTION),
                    Strategy = strategy,
                    Position = transform.position,
                    Forward = transform.forward,
                    DefendRadius = Mathf.Max(0f, defendRadius),
                    PokeAdvance = Mathf.Max(0f, pokeAdvance),
                    AggroRadius = Mathf.Max(0f, aggroRadius) });
        }
    }

    public struct Banner : IComponentData
    {
        public byte Faction;
        public BannerStrategy Strategy;
        public float3 Position;
        public float3 Forward;
        public float DefendRadius;
        public float PokeAdvance;
        public float AggroRadius;
    }
}