using GPUInstancerPro.CrowdAnimations;
using OneBitRob.AI;
using OneBitRob.Config;
using OneBitRob.EnigmaEngine;
using PROJECT.Scripts.Config.Definition;
using UnityEngine;

namespace OneBitRob.Anim
{
    [DisallowMultipleComponent]
    public sealed class UnitAnimator : MonoBehaviour
    {
        GPUICrowdInstance _crowd;
        UnitBrain _brain;

        LocomotionAnimationsDefinition _loc;
        float _blend;

        // ── CHANGED: private cursors (no public / no authoring)
        int _nextMelee, _nextRangedPrepare, _nextRangedFire, _nextSpell;

        void Awake()
        {
            _crowd = GetComponent<GPUICrowdInstance>();
            if (!_crowd) { Debug.LogError("[UnitAnimator] GPUICrowdInstance (Compute Animator) missing.", this); enabled = false; return; }

            _brain = GetComponent<UnitBrain>();
            var ud = _brain?.UnitDefinition;

            _loc = ud?.locomotionAnimations;
            _blend = (_loc ? Mathf.Max(0f, _loc.defaultBlendSeconds) : 0.12f);

            SeedCursors(); // ── NEW: deterministic per-instance start offsets
        }

        // ── NEW: deterministic seeding so many units don’t sync on the same clip
        void SeedCursors()
        {
            int seed = GetStableSeed();
            unchecked
            {
                _nextMelee         =  (seed * 1103515245 + 12345);
                _nextRangedPrepare =  (seed * 214013      + 2531011);
                _nextRangedFire    =  (seed * 48271) ^ (seed << 7);
                _nextSpell         =  (seed * 1597334677);
            }
        }

        int GetStableSeed()
        {
            // Prefer deterministic ECS entity data if available
            var ent = OneBitRob.AI.UnitBrainRegistry.GetEntity(gameObject);
            if (ent != Unity.Entities.Entity.Null)
                return (ent.Index ^ (ent.Version << 16) ^ GetInstanceID());
            return GetInstanceID();
        }

        public void PlayDeath()
        {
            if (_loc?.Death) StartAnim(_loc.Death, false, _blend);
        }

        public void PlayStunnedLoop(bool on)
        {
            if (on && _loc?.StunnedLoop) StartAnim(_loc.StunnedLoop, true, _blend);
        }

        public void ApplyMovement(EnigmaCharacterStates.MovementStates state, Vector3 velocity, float maxMoveSpeed)
        {
            if (_loc == null) return;

            if (state == EnigmaCharacterStates.MovementStates.CombatStance && _loc.CombatStanceRun)
            { StartAnim(_loc.CombatStanceRun, true, _blend); return; }

            if (_loc.Run && _loc.Idle)
            {
                bool moving = new Vector2(velocity.x, velocity.z).sqrMagnitude > 0.003f;
                StartAnim(moving ? _loc.Run : _loc.Idle, true, _blend);
                return;
            }

            if (_loc.Idle) StartAnim(_loc.Idle, true, _blend);
        }

        public void PlayMelee(ComputeAttackAnimationSettings set)
        {
            var clip = set?.Select(ref _nextMelee);
            if (clip) StartAnim(clip, false, _blend);
        }

        public void PlayRangedPrepare(ComputeTwoStageAttackAnimationSettings set)
        {
            var clip = set?.SelectPrepare(ref _nextRangedPrepare);
            if (clip) StartAnim(clip, false, _blend);
        }

        public void PlayRangedFire(ComputeTwoStageAttackAnimationSettings set)
        {
            var clip = set?.SelectFire(ref _nextRangedFire);
            if (clip) StartAnim(clip, false, _blend);
        }

        public void PlaySpell(ComputeAttackAnimationSettings set)
        {
            var clip = set?.Select(ref _nextSpell);
            if (clip) StartAnim(clip, false, _blend);
        }

        void StartAnim(AnimationClip clip, bool loop, float blend)
        {
            _crowd.StartAnimation(clip, -1f, 1f, blend, loop ? true : (bool?)null, false);
        }
    }
}
