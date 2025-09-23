using OneBitRob.ECS;
using OneBitRob.OneBitRob.Spawning;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.Spawning
{
    [DisallowMultipleComponent]
    public sealed class AdvancedSpawnerAuthoring : MonoBehaviour
    {
        [Header("Enemy Spawn Definition (Wave-based)")]
        public EnemyWavesDefinition enemySpawns;

        [Header("Allies Spawn Definition")]
        public AllySpawnDefinition allySpawns;
    }

    public sealed class SpawningAuthoringBaker : Baker<AdvancedSpawnerAuthoring>
    {
        public override void Bake(AdvancedSpawnerAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.None);

            if (authoring.enemySpawns != null)
            {
                AddComponentObject(e, new EnemySpawnDefinitionRef { EnemySpawnDefinition = authoring.enemySpawns });
            }

            if (authoring.allySpawns != null)
            {
                AddComponentObject(e, new AllySpawnDefinitionRef { AllySpawnDefinition = authoring.allySpawns });
                AddComponent(e, new AllySpawnTimer { Elapsed = 9999f });
            }
        }
    }
}