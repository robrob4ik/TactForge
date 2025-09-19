using UnityEngine;

namespace PROJECT.Scripts.Config.Definition
{
    [CreateAssetMenu(menuName = "TactForge/Definition/Locomotion Animations Definition", fileName = "LocomotionAnimations")]
    public sealed class LocomotionAnimationsDefinition : ScriptableObject
    {
        [Header("Movement (loops)")]
        public AnimationClip Idle;
        public AnimationClip Run;
        public AnimationClip CombatStanceRun;

        [Header("States")]
        public AnimationClip StunnedLoop;

        public AnimationClip Death;

        [Header("Blend")]
        [Min(0f)]
        public float defaultBlendSeconds = 0.12f;
    }
}