using UnityEngine;
using Sirenix.OdinInspector;
using OneBitRob.FX;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Weapon (Ranged)", fileName = "RangedWeaponDefinition")]
    public class RangedWeaponDefinition : WeaponDefinition
    {
        [BoxGroup("Muzzle")]
        [LabelText("Muzzle Forward (Local Z)"), SuffixLabel("units", true)]
        [InfoBox("Forward offset in local Z. Keep 0 if you use muzzleLocalOffset.z instead.", InfoMessageType.None)]
        [MinValue(0f)]
        public float muzzleForward = 0.60f;

        [BoxGroup("Muzzle")]
        [LabelText("Muzzle Local Offset (XYZ)")]
        public Vector3 muzzleLocalOffset = Vector3.zero;

        [BoxGroup("Projectile")]
        [LabelText("Speed"), SuffixLabel("units/s", true)]
        [MinValue(0.01f)]
        public float projectileSpeed = 40f;

        [BoxGroup("Projectile")]
        [LabelText("Max Distance"), SuffixLabel("units", true)]
        [MinValue(0.10f)]
        public float projectileMaxDistance = 40f;

        [BoxGroup("Timing")]
        [LabelText("Windup"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float windupSeconds = 0.5f;

        [BoxGroup("Animations")]
        public TwoStageAttackAnimationSettings animations;

        [BoxGroup("Projectile Pool Key")]
        [InfoBox("Key used to choose a scene pool in ProjectilePools (e.g. 'arrow', 'mage_orb').")]
        public string projectileId = "arrow";

        [BoxGroup("Feedbacks")]
        [LabelText("Prepare Feedback")]
        [AssetsOnly] public FeedbackDefinition prepareFeedback;

        [BoxGroup("Feedbacks")]
        [LabelText("Fire Feedback")]
        [AssetsOnly] public FeedbackDefinition fireFeedback;

        [BoxGroup("Feedbacks")]
        [LabelText("Impact Feedback")]            
        [AssetsOnly] public FeedbackDefinition impactFeedback;
    }
}
