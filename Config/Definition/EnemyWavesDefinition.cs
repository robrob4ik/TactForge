using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob
{
    namespace OneBitRob.Spawning
    {
        [CreateAssetMenu(menuName = "TactForge/Definition/Enemy Waves Definition", fileName = "EnemyWavesDefinition")]
        public sealed class EnemyWavesDefinition : ScriptableObject
        {
            [Min(0f)]
            public float interWaveDelaySeconds = 3f;
            
            public bool loop = false;
            public List<EnemyWaveConfig> waves = new();
        }

        [Serializable]
        public sealed class EnemyWaveConfig
        {
            public string name = "Wave";

            [Min(0.1f)]
            public float durationSeconds = 20f;
            public List<UnitWaveSpawnConfig> entries = new();
        }

        [Serializable]
        public sealed class UnitWaveSpawnConfig
        {
            public GameObject unitPrefab;

            [Min(1)]
            public int count = 10;

            [Min(0.1f), Tooltip("Spread this unit's spawns uniformly across this window inside the wave.")]
            public float windowSeconds = 10f;
        }
    }
}