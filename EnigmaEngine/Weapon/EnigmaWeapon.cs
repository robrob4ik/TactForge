using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [SelectionBase]
    public class EnigmaWeapon : MonoBehaviour
    {
        public enum TriggerModes { SemiAuto, Auto }
        public enum WeaponStates
        {
            WeaponIdle,
            WeaponStart,
            WeaponDelayBeforeUse,
            WeaponUse,
            WeaponDelayBetweenUses,
            WeaponStop,
            WeaponInterrupted
        }

        [Title("ID"), FoldoutGroup("ID")]
        public string WeaponName;

        [FoldoutGroup("ID"), ReadOnly]
        public bool WeaponCurrentlyActive = true;

        [Title("Use"), FoldoutGroup("Use")]
        [Tooltip("The layers that will be damaged by this weapon / its projectiles")]
        public LayerMask TargetLayerMask;

        [FoldoutGroup("Use")]
        public bool InputAuthorized = true;

        [FoldoutGroup("Use")]
        public TriggerModes TriggerMode = TriggerModes.Auto;

        [FoldoutGroup("Use")]
        [Tooltip("Delay applied before each shot (seconds)")]
        public float DelayBeforeUse = 0f;

        [FoldoutGroup("Use")]
        public bool DelayBeforeUseReleaseInterruption = true;

        [FoldoutGroup("Use")]
        [Tooltip("Cooldown between shots (seconds)")]
        public float TimeBetweenUses = 0.15f;

        [FoldoutGroup("Use")]
        public bool TimeBetweenUsesReleaseInterruption = true;

        [Title("Burst"), FoldoutGroup("Burst")]
        [Tooltip("If true, pressing fire triggers a short burst")]
        public bool UseBurstMode = false;

        [FoldoutGroup("Burst")]
        public int BurstLength = 3;

        [FoldoutGroup("Burst")]
        public float BurstTimeBetweenShots = 0.08f;

        [Title("Position"), FoldoutGroup("Position")]
        public Vector3 WeaponAttachmentOffset = Vector3.zero;

        [FoldoutGroup("Position")]
        public Transform WeaponUseTransform;

        [Title("Recoil"), FoldoutGroup("Recoil")]
        [Tooltip("Positive pushes the character backwards")]
        public float RecoilForce = 0f;

        [Title("Animation"), FoldoutGroup("Animation")]
        public List<Animator> Animators = new();

        [FoldoutGroup("Animation")]
        [Tooltip("ID passed to animator if you need it")]
        public int WeaponAnimationID = 0;

        [FoldoutGroup("Animation")]
        public string IdleAnimationParameter;
        [FoldoutGroup("Animation")]
        public string StartAnimationParameter;
        [FoldoutGroup("Animation")]
        public string SingleUseAnimationParameter;
        [FoldoutGroup("Animation")]
        public string UseAnimationParameter;
        [FoldoutGroup("Animation")]
        public string StopAnimationParameter;
        [FoldoutGroup("Animation")]
        public string EquippedAnimationParameter;
        [FoldoutGroup("Animation")]
        public string InterruptedAnimationParameter;

        [Title("Feedbacks"), FoldoutGroup("Feedbacks")]
        public MMFeedbacks WeaponStartMMFeedback;
        [FoldoutGroup("Feedbacks")]
        public MMFeedbacks WeaponUsedMMFeedback;
        [FoldoutGroup("Feedbacks")]
        public MMFeedbacks WeaponStopMMFeedback;

        [Title("Settings"), FoldoutGroup("Settings")]
        [Tooltip("If true, run Initialization() in Start()")]
        public bool InitializeOnStart = false;

        [FoldoutGroup("Settings")]
        public bool Interruptable = false;

        // Runtime
        public virtual string WeaponID { get; set; }
        public virtual EnigmaCharacter Owner { get; protected set; }
        public virtual EnigmaCharacterHandleWeapon CharacterHandleWeapon { get; protected set; }

        public MMStateMachine<WeaponStates> WeaponState;

        // Kept for compatibility with your other scripts
        public bool IsComboWeapon { get; private set; }
        public bool IsAutoComboWeapon { get; private set; }

        protected EnigmaWeaponAim _weaponAim;
        protected EnigmaController _controller;
        protected Animator _ownerAnimator;

        // anim caches (one hash set per animator)
        protected readonly List<HashSet<int>> _animatorParameters = new();
        protected HashSet<int> _ownerAnimatorParameters;

        protected int _idleParam, _startParam, _useParam, _singleUseParam, _stopParam, _equippedParam, _interruptedParam;

        protected float _delayBeforeUseCounter;
        protected float _delayBetweenUsesCounter;
        protected bool  _triggerReleased;

        protected float _lastShotAt = -999f;

        protected virtual void Start()
        {
            if (InitializeOnStart) { Initialization(); }
        }

        public virtual void Initialization()
        {
            WeaponState = new MMStateMachine<WeaponStates>(gameObject, true);
            WeaponState.ChangeState(WeaponStates.WeaponIdle);

            _weaponAim = GetComponent<EnigmaWeaponAim>();
            InitializeFeedbacks();
            InitializeAnimatorParameters();
        }

        protected virtual void InitializeFeedbacks()
        {
            WeaponStartMMFeedback?.Initialization(gameObject);
            WeaponUsedMMFeedback?.Initialization(gameObject);
            WeaponStopMMFeedback?.Initialization(gameObject);
        }

        // Kept for HandleWeapon calls – we don't do combo logic here
        public virtual void InitializeComboWeapons()
        {
            IsComboWeapon = false;
            IsAutoComboWeapon = false;
        }

        public virtual void SetOwner(EnigmaCharacter newOwner, EnigmaCharacterHandleWeapon handleWeapon)
        {
            Owner = newOwner;
            CharacterHandleWeapon = handleWeapon;
            _controller = Owner ? Owner.GetComponent<EnigmaController>() : null;

            // Try to bind a character animator once
            if (handleWeapon && handleWeapon.AutomaticallyBindAnimator)
            {
                if (handleWeapon.CharacterAnimator != null) _ownerAnimator = handleWeapon.CharacterAnimator;
                if (_ownerAnimator == null) _ownerAnimator = handleWeapon.gameObject.GetComponentInParent<Animator>();
            }
        }

        protected virtual void Update() { ApplyOffset(); }
        protected virtual void LateUpdate() { ProcessWeaponState(); UpdateAnimator(); }

        protected virtual void ProcessWeaponState()
        {
            switch (WeaponState.CurrentState)
            {
                case WeaponStates.WeaponIdle:
                    break;

                case WeaponStates.WeaponStart:
                    if (DelayBeforeUse > 0f)
                    {
                        _delayBeforeUseCounter = DelayBeforeUse;
                        WeaponState.ChangeState(WeaponStates.WeaponDelayBeforeUse);
                    }
                    else
                    {
                        StartCoroutine(ShootRoutine());
                    }
                    break;

                case WeaponStates.WeaponDelayBeforeUse:
                    if (_triggerReleased && DelayBeforeUseReleaseInterruption)
                    {
                        TurnWeaponOff();
                        break;
                    }
                    _delayBeforeUseCounter -= Time.deltaTime;
                    if (_delayBeforeUseCounter <= 0f)
                        StartCoroutine(ShootRoutine());
                    break;

                case WeaponStates.WeaponUse:
                    // Never stays in this state long; ShootRoutine bumps it
                    break;

                case WeaponStates.WeaponDelayBetweenUses:
                    if (_triggerReleased && TimeBetweenUsesReleaseInterruption)
                    {
                        TurnWeaponOff();
                        break;
                    }
                    _delayBetweenUsesCounter -= Time.deltaTime;
                    if (_delayBetweenUsesCounter <= 0f)
                    {
                        // Auto keeps going, Semi waits for next press
                        if (TriggerMode == TriggerModes.Auto && !_triggerReleased)
                            StartCoroutine(ShootRoutine());
                        else
                            TurnWeaponOff();
                    }
                    break;

                case WeaponStates.WeaponStop:
                    WeaponState.ChangeState(WeaponStates.WeaponIdle);
                    break;

                case WeaponStates.WeaponInterrupted:
                    TurnWeaponOff();
                    WeaponState.ChangeState(WeaponStates.WeaponIdle);
                    break;
            }
        }

        public virtual void WeaponInputStart()
        {
            if (!InputAuthorized || WeaponState.CurrentState != WeaponStates.WeaponIdle)
                return;

            _triggerReleased = false;
            TurnWeaponOn();
        }

        public virtual void WeaponInputStop()
        {
            _triggerReleased = true;
        }

        public virtual void WeaponInputReleased() { /* hook if you need */ }

        public virtual void TurnWeaponOn()
        {
            TriggerWeaponStartFeedback();
            WeaponState.ChangeState(WeaponStates.WeaponStart);
        }

        public virtual void TurnWeaponOff()
        {
            if (WeaponState.CurrentState == WeaponStates.WeaponIdle ||
                WeaponState.CurrentState == WeaponStates.WeaponStop)
                return;

            _triggerReleased = true;
            TriggerWeaponStopFeedback();
            WeaponState.ChangeState(WeaponStates.WeaponStop);
        }

        public virtual void Interrupt()
        {
            if (!Interruptable) return;
            WeaponState.ChangeState(WeaponStates.WeaponInterrupted);
        }

        // single place that actually performs a "shot"
        public virtual void WeaponUse()
        {
            ApplyRecoil();
            TriggerWeaponUsedFeedback();
        }

        protected virtual void ApplyRecoil()
        {
            if (RecoilForce == 0f || _controller == null || Owner == null)
                return;

            _controller.Impact(-transform.forward, RecoilForce);
        }

        protected IEnumerator ShootRoutine()
        {
            // cooldown gate
            if (Time.time - _lastShotAt < Mathf.Max(0.01f, TimeBetweenUses))
                yield break;

            int shots = UseBurstMode ? Mathf.Max(1, BurstLength) : 1;
            float between = UseBurstMode ? Mathf.Max(0f, BurstTimeBetweenShots) : 0f;

            for (int i = 0; i < shots; i++)
            {
                if (_triggerReleased && TriggerMode == TriggerModes.SemiAuto)
                    break;

                // fire once
                _lastShotAt = Time.time;
                WeaponState.ChangeState(WeaponStates.WeaponUse);
                WeaponUse();

                // schedule inter-shot or exit
                if (i < shots - 1)
                    yield return new WaitForSeconds(between);
            }

            _delayBetweenUsesCounter = Mathf.Max(0.01f, TimeBetweenUses);
            WeaponState.ChangeState(WeaponStates.WeaponDelayBetweenUses);
        }

        public virtual void ApplyOffset()
        {
            if (!WeaponCurrentlyActive) return;
            if (transform.parent == null) return;

            transform.localPosition = WeaponAttachmentOffset;
        }

        protected virtual void TriggerWeaponStartFeedback() => WeaponStartMMFeedback?.PlayFeedbacks(transform.position);
        protected virtual void TriggerWeaponUsedFeedback()  => WeaponUsedMMFeedback?.PlayFeedbacks(transform.position);
        protected virtual void TriggerWeaponStopFeedback()  => WeaponStopMMFeedback?.PlayFeedbacks(transform.position);

        // -------- Animator (minimal) --------
        public virtual void InitializeAnimatorParameters()
        {
            _ownerAnimatorParameters = null;

            if (_ownerAnimator != null)
            {
                _ownerAnimatorParameters = new HashSet<int>();
                AddParametersToAnimator(_ownerAnimator, _ownerAnimatorParameters);
            }

            _animatorParameters.Clear();
            for (int i = 0; i < Animators.Count; i++)
            {
                var set = new HashSet<int>();
                _animatorParameters.Add(set);
                AddParametersToAnimator(Animators[i], set);
            }
        }

        protected void AddParametersToAnimator(Animator animator, HashSet<int> list)
        {
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, EquippedAnimationParameter, out _equippedParam, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, IdleAnimationParameter,      out _idleParam,     AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, StartAnimationParameter,     out _startParam,    AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, UseAnimationParameter,       out _useParam,      AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, SingleUseAnimationParameter, out _singleUseParam,AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, StopAnimationParameter,      out _stopParam,     AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, InterruptedAnimationParameter, out _interruptedParam, AnimatorControllerParameterType.Trigger, list);
        }

        public virtual void UpdateAnimator()
        {
            // owner animator
            if (_ownerAnimator != null && _ownerAnimatorParameters != null)
            {
                UpdateAnimator(_ownerAnimator, _ownerAnimatorParameters);
            }

            // extra animators
            for (int i = 0; i < Animators.Count; i++)
            {
                var a = Animators[i];
                var set = (i < _animatorParameters.Count) ? _animatorParameters[i] : null;
                if (a != null && set != null)
                    UpdateAnimator(a, set);
            }
        }

        protected void UpdateAnimator(Animator animator, HashSet<int> list)
        {
            if (_equippedParam != 0)
                MMAnimatorExtensions.UpdateAnimatorBool(animator, _equippedParam, true, list);

            if (_idleParam != 0)
                MMAnimatorExtensions.UpdateAnimatorBool(animator, _idleParam, WeaponState.CurrentState == WeaponStates.WeaponIdle, list);

            if (_startParam != 0)
                MMAnimatorExtensions.UpdateAnimatorBool(animator, _startParam, WeaponState.CurrentState == WeaponStates.WeaponStart, list);

            if (_useParam != 0)
            {
                bool inUse = WeaponState.CurrentState == WeaponStates.WeaponUse
                          || WeaponState.CurrentState == WeaponStates.WeaponDelayBetweenUses
                          || WeaponState.CurrentState == WeaponStates.WeaponDelayBeforeUse;
                MMAnimatorExtensions.UpdateAnimatorBool(animator, _useParam, inUse, list);
            }

            if (_singleUseParam != 0)
                MMAnimatorExtensions.UpdateAnimatorBool(animator, _singleUseParam, WeaponState.CurrentState == WeaponStates.WeaponUse, list);

            if (_stopParam != 0)
                MMAnimatorExtensions.UpdateAnimatorBool(animator, _stopParam, WeaponState.CurrentState == WeaponStates.WeaponStop, list);

            if (WeaponState.CurrentState == WeaponStates.WeaponInterrupted && _interruptedParam != 0)
                MMAnimatorExtensions.UpdateAnimatorTrigger(animator, _interruptedParam, list);
        }

        public virtual void InitiateReloadWeapon() { }
    }
}
