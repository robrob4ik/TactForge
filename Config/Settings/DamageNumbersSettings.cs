// Runtime/FX/DamageNumbersProfile.cs
using DamageNumbersPro;
using UnityEngine;

namespace OneBitRob.FX
{
    [CreateAssetMenu(menuName = "TactForge/Damage Numbers Profile", fileName = "DamageNumbersProfile")]
    public class DamageNumbersSettings : ScriptableObject
    {
        [Header("Prefabs (Mesh / Worldspace)")]
        public DamageNumber damagePrefab;
        public DamageNumber critPrefab;
        public DamageNumber healPrefab;
        public DamageNumber dotPrefab;
        public DamageNumber hotPrefab;
        public DamageNumber blockPrefab;
        public DamageNumber missPrefab;

        [Header("Lifecycle & Pooling")]
        public bool prewarmOnStart = true;
        [Min(0)] public int extraPrewarmCalls = 0;

        [Header("Placement")]
        [Tooltip("Vertical offset added to world pos if no follow target is given.")]
        public float yOffset = 1.2f;

        [Header("Following")]
        public bool followTargets = true;

        [Header("Culling")]
        public bool cullByCameraDistance = true;
        public float maxSpawnDistance = 60f;
        
        [Header("Filtering")]
        [Tooltip("Don’t spawn popups whose absolute value is below this.")]
        public float minAbsoluteValue = 0.5f;

        [Header("Debug")]
        public bool logMissingPrefabWarnings = true;
    }
}