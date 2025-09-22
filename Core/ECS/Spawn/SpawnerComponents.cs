using OneBitRob.OneBitRob.Spawning;
using OneBitRob.Spawning;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.ECS
{
    public struct SpawnerMarker : IComponentData { public SpawnerType Type; }
    public struct SpawnerTimer  : IComponentData { public float ElapsedTime; }
    public class  SpawnerData   : IComponentData
    {
        public Entity EntityPrefab;
        public GameObject[] EnemyPrefabs;
        public GameObject[] AllyPrefabs;
        public int UnitsSpawnCount;
        public int SpawnFrequency;
        public Vector3 SpawnAreaFrom, SpawnAreaTo;
    }
    
    public struct AllySpawnTimer : IComponentData { public float Elapsed; }
    
    public sealed class AllySpawnSetRef : IComponentData { public AllySpawnDefinition Set; }

    public sealed class EnemyWavesRef : IComponentData
    {
        public EnemyWavesDefinition Waves;
    }

    public struct EnemyWaveRuntime : IComponentData
    {
        public int WaveIndex;
        public float WaveElapsed;
        public float InterDelayRemaining;
        public byte Active;
    }

    public struct EnemyWaveEntryCursor : IBufferElementData
    {
        public int Remaining;
        public float Accum; // fractional spawn progress
        public float RatePerSec;
        public float Window; // seconds
    }

}