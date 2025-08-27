// FILE: Runtime/Combat/MeleeWeaponDefinition.cs
using UnityEngine;
using Sirenix.OdinInspector;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Weapon (Melee)", fileName = "MeleeWeaponDefinition")]
    public class MeleeWeaponDefinition : WeaponDefinition
    {
        // Arc / Targets
        [BoxGroup("Arc & Targets")]
        [LabelText("Half Angle"), PropertyRange(0f, 179f), SuffixLabel("°", true)]
        public float halfAngleDeg = 60f;

        [BoxGroup("Arc & Targets")]
        [MinValue(1)]
        public int   maxTargets   = 3;

        [BoxGroup("Arc & Targets")]
        [LabelText("Invincibility After Hit"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float invincibility = 0.10f;

        // Animations
        [BoxGroup("Animations")]
        public AttackAnimationSettings attackAnimations;
    }
}