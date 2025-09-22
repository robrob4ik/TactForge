using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.Spawning
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Ally Spawn Definition", fileName = "AllySpawnDefinition")]
    public sealed class AllySpawnDefinition : ScriptableObject
    {
        [Min(1f)]
        public float periodSeconds = 30f;

        public List<AllySpawnConfig> entries = new();
    }

    [Serializable]
    public sealed class AllySpawnConfig
    {
        public GameObject unitPrefab;

        [Min(1)]
        public int count = 1;
    }
}