// Runtime/Combat/MeleeWeaponDefinition.cs
using UnityEngine;

namespace OneBitRob
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Weapon (Melee)", fileName = "MeleeWeaponDefinition")]
    public class MeleeWeaponDefinition : WeaponDefinition
    {
        [Range(0f, 179f)] public float halfAngleDeg = 60f;
        [Min(1)]          public int   maxTargets   = 3;
        [Min(0f)]         public float invincibility = 0.10f;

        [Header("Animations")]
        public AttackAnimationSettings attackAnimations;
    }
}