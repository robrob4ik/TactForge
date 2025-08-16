using UnityEngine;

namespace OneBitRob
{
    public abstract class WeaponDefinition : ScriptableObject
    {
        [Header("Common")]
        public float attackRange = 1.5f;
        public float attackDamage = 10f;
        public float attackCooldown = 0.5f;

        [Header("Variance")]
        [Tooltip("Random +/- added to attackCooldown each use. Example: 0.05 -> 1s becomes [0.95..1.05]s")]
        public float attackCooldownJitter = 0f;

        [Header("Criticals")]
        [Range(0f, 1f)] public float critChance = 0f;
        [Min(1f)] public float critMultiplier = 2f;
    }
}