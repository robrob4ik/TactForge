
namespace OneBitRob.ECS
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "TactForge/Config/Spawner Config")]
    public class SpawnerSettings : ScriptableObject
    {
        [Tooltip("Allies prefabs to spawn")]
        [SerializeField] protected GameObject[] m_AllyPrefabs;
        [Tooltip("Enemies prefabs to spawn")]
        [SerializeField] protected GameObject[] m_EnemyPrefabs;
        [Tooltip("Prefab reference ECS behavior tree entity")]
        [SerializeField] protected GameObject m_EntityPrefab;

        [Tooltip("Spawn count in spawn-tick")]
        [SerializeField] protected int m_UnitsSpawnCount;
        [Tooltip("Frequency of spawning units")]
        [SerializeField] protected int m_SpawningFrequency;
        [Tooltip("The From area that the entities should be spawned.")]
        [SerializeField] protected Vector3 m_SpawnAreaFrom = new Vector3(-10, 0, -10);
        [Tooltip("The To area that the entities should be spawned.")]
        [SerializeField] protected Vector3 m_SpawnAreaTo = new Vector3(10, 0, 10);

        public GameObject[] AllyPrefabs => m_AllyPrefabs;
        public GameObject[] EnemyPrefabs => m_EnemyPrefabs;
        public GameObject EntityPrefab => m_EntityPrefab;
        public int UnitsSpawnCount => m_UnitsSpawnCount;
        public int SpawningFrequency => m_SpawningFrequency;
        public Vector3 SpawnAreaFrom => m_SpawnAreaFrom;
        public Vector3 SpawnAreaTo => m_SpawnAreaTo;
    }
}