using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    public struct SpawnerMarker : IComponentData
    {
        public SpawnerType Type;
    }
    
    public struct SpawnerTimer : IComponentData
    {
        public float ElapsedTime;
    }

    public class SpawnerData : IComponentData
    {
        public Entity EntityPrefab;
        public GameObject[] EnemyPrefabs;
        public GameObject[] AllyPrefabs;
        public int UnitsSpawnCount;
        public int SpawnFrequency;
        public Vector3 SpawnAreaFrom, SpawnAreaTo;
    }
}