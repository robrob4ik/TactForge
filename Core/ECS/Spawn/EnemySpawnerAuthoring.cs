using OneBitRob.OneBitRob.Spawning;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    
    public sealed class EnemySpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] private EnemyWavesDefinition m_EnemyWavesSpawnDefinition;

        [Header("Agent ECS Shell (prefab produced by your baker)")]
        [SerializeField] private GameObject m_AgentEntityPrefab;
        
        [Header("Spawn Area Local Offsets")]
        [SerializeField] private Vector3 m_SpawnAreaFrom = new(-1, 0, -1);
        [SerializeField] private Vector3 m_SpawnAreaTo   = new( 1, 0,  1);

        private class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);

                if (authoring.m_EnemyWavesSpawnDefinition != null)
                    AddComponentObject(e, new EnemySpawnDefinitionRef { EnemySpawnDefinition = authoring.m_EnemyWavesSpawnDefinition });

                var agentPrefab = authoring.m_AgentEntityPrefab != null
                    ? GetEntity(authoring.m_AgentEntityPrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                AddComponent(e, new EnemyWavesConfig
                {
                    AgentEntityPrefab = agentPrefab,
                    SpawnAreaFrom = (float3)authoring.m_SpawnAreaFrom,
                    SpawnAreaTo   = (float3)authoring.m_SpawnAreaTo
                });
            }
        }
    }
}