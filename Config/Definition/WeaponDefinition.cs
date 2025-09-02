using UnityEngine;
using Sirenix.OdinInspector;

namespace OneBitRob
{
    public abstract class WeaponDefinition : ScriptableObject
    {
        [BoxGroup("Common")]
        [LabelText("Attack Range"), SuffixLabel("units", true)]
        [MinValue(0f)]
        public float attackRange = 1.5f;

        [BoxGroup("Common")]
        [LabelText("Attack Damage")]
        public float attackDamage = 10f;

        [BoxGroup("Common")]
        [LabelText("Attack Cooldown"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float attackCooldown = 1f;

        [BoxGroup("Variance")]
        [LabelText("Cooldown Jitter"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float attackCooldownJitter = 0.2f;

        [BoxGroup("Criticals")]
        [LabelText("Crit Chance"), PropertyRange(0f, 1f)]
        public float critChance = 0f;

        [BoxGroup("Criticals")]
        [LabelText("Crit Multiplier"), MinValue(1f), SuffixLabel("x", true)]
        public float critMultiplier = 2f;
    }
}