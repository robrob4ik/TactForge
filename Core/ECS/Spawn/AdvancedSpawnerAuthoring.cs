using OneBitRob.ECS;
using OneBitRob.OneBitRob.Spawning;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.Spawning
{
    [DisallowMultipleComponent]
    public sealed class AdvancedSpawnerAuthoring : MonoBehaviour
    {
        [Header("Enemy Waves (optional)")]
        public EnemyWavesDefinition enemyWaves;

        [Header("Allies Periodic Spawns (optional)")]
        public AllySpawnDefinition allySpawns;
    }

    public sealed class SpawningAuthoringBaker : Baker<AdvancedSpawnerAuthoring>
    {
        public override void Bake(AdvancedSpawnerAuthoring authoring)
        {
            // Single runtime entity carrying the managed refs/timer
            var e = GetEntity(TransformUsageFlags.None);

            if (authoring.enemyWaves != null)
            {
                AddComponentObject(e, new EnemyWavesRef { Waves = authoring.enemyWaves });
            }

            if (authoring.allySpawns != null)
            {
                AddComponentObject(e, new AllySpawnSetRef { Set = authoring.allySpawns });
                // Initialize to force an immediate first ally spawn on play
                AddComponent(e, new AllySpawnTimer { Elapsed = 9999f });
            }
        }
    }
}