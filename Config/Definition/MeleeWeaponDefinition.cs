using UnityEngine;
using Sirenix.OdinInspector;
using OneBitRob.FX;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Melee Weapon Definition", fileName = "MeleeWeaponDefinition")]
    public class MeleeWeaponDefinition : WeaponDefinition
    {
        [BoxGroup("Arc & Targets")]
        [LabelText("Half Angle"), PropertyRange(0f, 179f), SuffixLabel("°", true)]
        public float halfAngleDeg = 60f;

        [BoxGroup("Arc & Targets")]
        [MinValue(1)]
        public int maxTargets = 3;

        [BoxGroup("Arc & Targets")]
        [LabelText("Invincibility After Hit"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float invincibility = 0.10f;

        [BoxGroup("Animations")]
        public AttackAnimationSettings attackAnimations;

        [BoxGroup("Feedbacks")]
        [LabelText("Attack (Swing) Feedback")]
        [AssetsOnly] public FeedbackDefinition attackFeedback;

        [BoxGroup("Feedbacks")]
        [LabelText("Hit Feedback")]
        [AssetsOnly] public FeedbackDefinition hitFeedback;

        [BoxGroup("Timing")]
        [LabelText("Swing Lock"), SuffixLabel("s", true)]
        [MinValue(0f)]
        [Tooltip("Locks movement for this duration after a melee strike to prevent sliding/orbiting while attacking.")]
        public float swingLockSeconds = 0.25f;
    }
}