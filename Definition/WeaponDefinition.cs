using UnityEngine;

namespace OneBitRob
{
    public abstract class WeaponDefinition : ScriptableObject
    {
        [Header("Common")]
        public float attackRange = 1.5f;
        public float attackDamage = 10f;
        public float attackCooldown = 0.5f;
    }




}