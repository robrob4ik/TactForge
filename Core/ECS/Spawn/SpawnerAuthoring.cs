namespace OneBitRob.ECS
{
    using Unity.Entities;
    using UnityEngine;

    public class SpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] protected SpawnerSettings m_SpawnDefinition;

        private class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                if (authoring.m_SpawnDefinition == null || (authoring.m_SpawnDefinition.EnemyPrefabs == null && authoring.m_SpawnDefinition.AllyPrefabs == null))
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var entityPrefab = GetEntity(authoring.m_SpawnDefinition.EntityPrefab, TransformUsageFlags.Dynamic);

                AddComponentObject(entity, new SpawnerData
                {
                    EntityPrefab = entityPrefab,
                    EnemyPrefabs = authoring.m_SpawnDefinition.EnemyPrefabs,
                    AllyPrefabs = authoring.m_SpawnDefinition.AllyPrefabs,
                    UnitsSpawnCount = authoring.m_SpawnDefinition.UnitsSpawnCount,
                    SpawnFrequency = authoring.m_SpawnDefinition.SpawningFrequency,
                    SpawnAreaFrom = authoring.m_SpawnDefinition.SpawnAreaFrom,
                    SpawnAreaTo = authoring.m_SpawnDefinition.SpawnAreaTo,
                });
                
                AddComponent(entity, new SpawnerTimer { ElapsedTime = 999999f });
            }
        }
    }
}