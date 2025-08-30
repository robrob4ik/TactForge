using UnityEngine;
using Sirenix.OdinInspector;
using OneBitRob.FX;

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

        [BoxGroup("Timing")]
        [LabelText("Lock While Firing"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float lockWhileFiringSeconds = 0.25f;

        // Animations (swing)
        [BoxGroup("Animations")]
        public AttackAnimationSettings attackAnimations;

        // Feedbacks
        [BoxGroup("Feedbacks")]
        [LabelText("Attack (Swing) Feedback")]
        [AssetsOnly] public FeedbackDefinition attackFeedback;

        [BoxGroup("Feedbacks")]
        [LabelText("Hit Feedback")]                   // NEW: plays when the melee attack actually lands
        [AssetsOnly] public FeedbackDefinition hitFeedback;
    }
}