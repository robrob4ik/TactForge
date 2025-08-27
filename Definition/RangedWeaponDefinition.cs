// FILE: Runtime/Combat/RangedWeaponDefinition.cs
using UnityEngine;
using Sirenix.OdinInspector;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Weapon (Ranged)", fileName = "RangedWeaponDefinition")]
    public class RangedWeaponDefinition : WeaponDefinition
    {
        // Muzzle
        [BoxGroup("Muzzle")]
        [LabelText("Muzzle Forward (Local Z)"), SuffixLabel("units", true)]
        [InfoBox("Forward offset in local Z. Keep 0 if you use muzzleLocalOffset.z instead.", InfoMessageType.None)]
        [MinValue(0f)]
        public float muzzleForward = 0.60f;

        [BoxGroup("Muzzle")]
        [LabelText("Muzzle Local Offset (XYZ)")]
        public Vector3 muzzleLocalOffset = Vector3.zero;

        // Projectile
        [BoxGroup("Projectile")]
        [LabelText("Speed"), SuffixLabel("units/s", true)]
        [MinValue(0.01f)]
        public float projectileSpeed = 60f;

        [BoxGroup("Projectile")]
        [LabelText("Max Distance"), SuffixLabel("units", true)]
        [MinValue(0.10f)]
        public float projectileMaxDistance = 40f;

        // Timing
        [BoxGroup("Timing")]
        [LabelText("Windup"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float windupSeconds = 0.15f;

        // Animations
        [BoxGroup("Animations")]
        public TwoStageAttackAnimationSettings animations;

        // Projectile Pool Key
        [BoxGroup("Projectile Pool Key")]
        [InfoBox("Key used to choose a scene pool in ProjectilePools (e.g. 'arrow', 'mage_orb').")]
        public string projectileId = "arrow";
    }
}