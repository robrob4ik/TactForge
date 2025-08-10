using UnityEngine;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "SO/Combat/Weapon (Ranged)")]
    public class RangedWeaponDefinition : WeaponDefinition
    {
        [Min(0f)]
        public float muzzleForward = 0.60f;

        [Min(0.01f)]
        public float projectileSpeed = 60f;

        [Min(0.10f)]
        public float projectileMaxDistance = 40f;

        [Header("Timing")]
        [Min(0f)]
        public float windupSeconds = 0.15f; // time between prepare and release

        [Header("Animations (Two-Stage)")]
        public TwoStageAttackAnimationSet animations;
    }
}