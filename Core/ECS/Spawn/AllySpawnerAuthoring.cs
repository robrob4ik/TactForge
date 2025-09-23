using OneBitRob.Spawning;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    
    public sealed class AllySpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private AllySpawnDefinition m_SpawnDefinition;

        [Header("Agent ECS Shell (prefab produced by your baker)")]
        [SerializeField] private GameObject m_AgentEntityPrefab;

        [Header("Spawn Area Local Offsets")]
        [SerializeField] private Vector3 m_SpawnAreaFrom = new(-1, 0, -1);
        [SerializeField] private Vector3 m_SpawnAreaTo   = new( 1, 0,  1);
        
        private class Baker : Baker<AllySpawnerAuthoring>
        {
            public override void Bake(AllySpawnerAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);

                if (authoring.m_SpawnDefinition != null)
                    AddComponentObject(e, new AllySpawnDefinitionRef { AllySpawnDefinition = authoring.m_SpawnDefinition });

                AddComponent(e, new AllySpawnTimer { Elapsed = 0f });

                // NEW: bake config (separate from SpawnerSettings)
                var agentPrefab = authoring.m_AgentEntityPrefab != null
                    ? GetEntity(authoring.m_AgentEntityPrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                AddComponent(e, new AllyKeepConfig
                {
                    AgentEntityPrefab = agentPrefab,
                    SpawnAreaFrom = (float3)authoring.m_SpawnAreaFrom,
                    SpawnAreaTo   = (float3)authoring.m_SpawnAreaTo
                });
            }
        }
    }
}