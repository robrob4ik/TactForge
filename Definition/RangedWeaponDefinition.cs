// FILE: OneBitRob/RangedWeaponDefinition.cs
using UnityEngine;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "SO/Combat/Weapon (Ranged)")]
    public class RangedWeaponDefinition : WeaponDefinition
    {
        [Header("Muzzle")]
        [Tooltip("Forward offset in local Z. Keep 0 if you use muzzleLocalOffset.z instead.")]
        [Min(0f)] public float muzzleForward = 0.60f;

        [Tooltip("Local-space offset (X,Y,Z) from the character root/aim. X=right, Y=up, Z=forward.")]
        public Vector3 muzzleLocalOffset = Vector3.zero;

        [Header("Projectile")]
        [Min(0.01f)] public float projectileSpeed       = 60f;
        [Min(0.10f)] public float projectileMaxDistance = 40f;

        [Header("Timing")]
        [Min(0f)] public float windupSeconds = 0.15f;

        [Header("Animations (Two-Stage)")]
        public TwoStageAttackAnimationSet animations;

        [Header("Projectile Pool Key")]
        [Tooltip("Key used to choose a scene pool in ProjectilePools (e.g. 'arrow', 'mage_orb').")]
        public string projectileId = "arrow";
    }
}