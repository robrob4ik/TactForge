// FILE: Runtime/Combat/WeaponDefinition.cs
using UnityEngine;
using Sirenix.OdinInspector;

namespace OneBitRob
{
    public abstract class WeaponDefinition : ScriptableObject
    {
        // Common
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
        public float attackCooldown = 0.5f;

        // Variance
        [BoxGroup("Variance")]
        [InfoBox("Random ± added to attackCooldown each use. Example: 0.05 → 1s becomes [0.95..1.05]s.", InfoMessageType.None)]
        [LabelText("Cooldown Jitter"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float attackCooldownJitter = 0f;

        // Criticals
        [BoxGroup("Criticals")]
        [LabelText("Crit Chance"), PropertyRange(0f, 1f)]
        public float critChance = 0f;

        [BoxGroup("Criticals")]
        [LabelText("Crit Multiplier"), MinValue(1f), SuffixLabel("x", true)]
        public float critMultiplier = 2f;
    }
}