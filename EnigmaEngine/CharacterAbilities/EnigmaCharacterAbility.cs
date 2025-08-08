using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using Unity.Entities;

namespace OneBitRob.EnigmaEngine
{
    [TemporaryBakingType]
    public class EnigmaCharacterAbility : MonoBehaviour
    {
        [Title("General")]
        [FoldoutGroup("Feedbacks", expanded: false)]
        [Tooltip("The sound fx to play when the ability starts")]
        public AudioClip AbilityStartSfx;
        
        [FoldoutGroup("Feedbacks")]
        [Tooltip("The sound fx to play while the ability is running")]
        public AudioClip AbilityInProgressSfx;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The sound fx to play when the ability stops")]
        public AudioClip AbilityStopSfx;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The feedbacks to play when the ability starts")]
        public MMFeedbacks AbilityStartFeedbacks;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The feedbacks to play when the ability stops")]
        public MMFeedbacks AbilityStopFeedbacks;

        [FoldoutGroup("Permission", expanded: false)]
        [Tooltip("If true, this ability can perform as usual, if not, it'll be ignored. You can use this to unlock abilities over time for example")]
        public bool AbilityPermitted = true;

        [FoldoutGroup("Permission")]
        [Tooltip("An array containing all the blocking movement states. If the Character is in one of these states and tries to trigger this ability, it won't be permitted. Useful to prevent this ability from being used while Idle or Swimming, for example.")]
        public EnigmaCharacterStates.MovementStates[] BlockingMovementStates;

        [FoldoutGroup("Permission")]
        [Tooltip("An array containing all the blocking condition states. If the Character is in one of these states and tries to trigger this ability, it won't be permitted. Useful to prevent this ability from being used while dead, for example.")]
        public EnigmaCharacterStates.CharacterConditions[] BlockingConditionStates;

        [FoldoutGroup("Permission")]
        [Tooltip("An array containing all the blocking weapon states. If one of the character's weapons is in one of these states and yet the character tries to trigger this ability, it won't be permitted. Useful to prevent this ability from being used while attacking, for example.")]
        public EnigmaWeapon.WeaponStates[] BlockingWeaponStates;

        public virtual bool AbilityAuthorized
        {
            get
            {
                if (_character != null)
                {
                    if ((BlockingMovementStates != null) && (BlockingMovementStates.Length > 0))
                    {
                        for (int i = 0; i < BlockingMovementStates.Length; i++)
                        {
                            if (BlockingMovementStates[i] == (_character.MovementState.CurrentState))
                            {
                                return false;
                            }
                        }
                    }

                    if ((BlockingConditionStates != null) && (BlockingConditionStates.Length > 0))
                    {
                        for (int i = 0; i < BlockingConditionStates.Length; i++)
                        {
                            if (BlockingConditionStates[i] == (_character.ConditionState.CurrentState))
                            {
                                return false;
                            }
                        }
                    }

                    if ((BlockingWeaponStates != null) && (BlockingWeaponStates.Length > 0))
                    {
                        for (int i = 0; i < BlockingWeaponStates.Length; i++)
                        {
                            foreach (EnigmaCharacterHandleWeapon handleWeapon in _handleWeaponList)
                            {
                                if (handleWeapon.CurrentWeapon != null)
                                {
                                    if (BlockingWeaponStates[i] ==
                                        (handleWeapon.CurrentWeapon.WeaponState.CurrentState))
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }

                return AbilityPermitted;
            }
        }

        public virtual bool AbilityInitialized
        {
            get { return _abilityInitialized; }
        }

        public delegate void AbilityEvent();

        public AbilityEvent OnAbilityStart;
        public AbilityEvent OnAbilityStop;

        protected EnigmaCharacter _character;
        protected EnigmaController _controller;
        protected EnigmaController3D _controller3D;
        protected GameObject _model;
        protected EnigmaHealth _health;
        protected EnigmaCharacterMovement _characterMovement;
        protected EnigmaInputManager _inputManager;
        protected Animator _animator = null;
        protected EnigmaCharacterStates _state;
        protected SpriteRenderer _spriteRenderer;
        protected MMStateMachine<EnigmaCharacterStates.MovementStates> _movement;
        protected MMStateMachine<EnigmaCharacterStates.CharacterConditions> _condition;
        protected AudioSource _abilityInProgressSfx;
        protected bool _abilityInitialized = false;
        protected float _verticalInput;
        protected float _horizontalInput;
        protected bool _startFeedbackIsPlaying = false;
        protected List<EnigmaCharacterHandleWeapon> _handleWeaponList;

        public virtual string HelpBoxText()
        {
            return "";
        }

        protected virtual void Awake()
        {
            PreInitialization();
        }

        protected virtual void Start()
        {
            Initialization();
        }

        protected virtual void PreInitialization()
        {
            _character = this.gameObject.GetComponentInParent<EnigmaCharacter>();
            BindAnimator();
        }

        protected virtual void Initialization()
        {
            BindAnimator();
            _controller = this.gameObject.GetComponentInParent<EnigmaController>();
            _controller3D = this.gameObject.GetComponentInParent<EnigmaController3D>();
            _model = _character.CharacterModel;
            _characterMovement = _character?.FindAbility<EnigmaCharacterMovement>();
            _spriteRenderer = this.gameObject.GetComponentInParent<SpriteRenderer>();
            _health = _character.CharacterHealth;
            _handleWeaponList = _character?.FindAbilities<EnigmaCharacterHandleWeapon>();
            _inputManager = _character.LinkedInputManager;
            _state = _character.enigmaCharacterState;
            _movement = _character.MovementState;
            _condition = _character.ConditionState;
            _abilityInitialized = true;
        }

        public virtual void ForceInitialization()
        {
            Initialization();
        }

        protected virtual void BindAnimator()
        {
            if (_character._animator == null)
            {
                _character.AssignAnimator();
            }

            _animator = _character._animator;

            if (_animator != null)
            {
                InitializeAnimatorParameters();
            }
        }


        protected virtual void InitializeAnimatorParameters() { }

        protected virtual void InternalHandleInput()
        {
            if (_inputManager == null)
            {
                return;
            }

            _horizontalInput = _inputManager.PrimaryMovement.x;
            _verticalInput = _inputManager.PrimaryMovement.y;
            HandleInput();
        }

        protected virtual void HandleInput() { }

        public virtual void ResetInput()
        {
            _horizontalInput = 0f;
            _verticalInput = 0f;
        }

        public virtual void EarlyProcessAbility()
        {
            InternalHandleInput();
        }

        public virtual void ProcessAbility() { }

        public virtual void LateProcessAbility() { }

        public virtual void UpdateAnimator() { }

        public virtual void PermitAbility(bool abilityPermitted)
        {
            AbilityPermitted = abilityPermitted;
        }

        public virtual void Flip() { }

        public virtual void ResetAbility() { }

        public virtual void SetInputManager(EnigmaInputManager newInputManager)
        {
            _inputManager = newInputManager;
        }

        public virtual void PlayAbilityStartSfx()
        {
            if (AbilityStartSfx != null)
            {
                AudioSource tmp = new AudioSource();
                MMSoundManagerSoundPlayEvent.Trigger(AbilityStartSfx, MMSoundManager.MMSoundManagerTracks.Sfx,
                    this.transform.position);
            }
        }

        public virtual void PlayAbilityUsedSfx()
        {
            if (AbilityInProgressSfx != null)
            {
                if (_abilityInProgressSfx == null)
                {
                    _abilityInProgressSfx = MMSoundManagerSoundPlayEvent.Trigger(AbilityInProgressSfx,
                        MMSoundManager.MMSoundManagerTracks.Sfx, this.transform.position, true);
                }
            }
        }

        public virtual void StopAbilityUsedSfx()
        {
            if (_abilityInProgressSfx != null)
            {
                MMSoundManagerSoundControlEvent.Trigger(MMSoundManagerSoundControlEventTypes.Free, 0,
                    _abilityInProgressSfx);
                _abilityInProgressSfx = null;
            }
        }

        public virtual void PlayAbilityStopSfx()
        {
            if (AbilityStopSfx != null)
            {
                MMSoundManagerSoundPlayEvent.Trigger(AbilityStopSfx, MMSoundManager.MMSoundManagerTracks.Sfx,
                    this.transform.position);
            }
        }

        public virtual void PlayAbilityStartFeedbacks()
        {
            AbilityStartFeedbacks?.PlayFeedbacks(this.transform.position);
            _startFeedbackIsPlaying = true;
            OnAbilityStart?.Invoke();
        }

        public virtual void StopStartFeedbacks()
        {
            AbilityStartFeedbacks?.StopFeedbacks();
            _startFeedbackIsPlaying = false;
        }

        public virtual void PlayAbilityStopFeedbacks()
        {
            AbilityStopFeedbacks?.PlayFeedbacks();
            OnAbilityStop?.Invoke();
        }

        protected virtual void RegisterAnimatorParameter(string parameterName,
            AnimatorControllerParameterType parameterType, out int parameter)
        {
            parameter = Animator.StringToHash(parameterName);

            if (_animator == null)
            {
                return;
            }

            if (_animator.MMHasParameterOfType(parameterName, parameterType))
            {
                if (_character != null)
                {
                    _character._animatorParameters.Add(parameter);
                }
            }
        }

        protected virtual void OnRespawn() { }

        protected virtual void OnDeath()
        {
            StopAbilityUsedSfx();
            StopStartFeedbacks();
        }

        protected virtual void OnHit() { }

        protected virtual void OnEnable()
        {
            if (_health == null)
            {
                _health = this.gameObject.GetComponentInParent<EnigmaCharacter>().CharacterHealth;
            }

            if (_health == null)
            {
                _health = this.gameObject.GetComponentInParent<EnigmaHealth>();
            }

            if (_health != null)
            {
                _health.OnRevive += OnRespawn;
                _health.OnDeath += OnDeath;
                _health.OnHit += OnHit;
            }
        }

        protected virtual void OnDisable()
        {
            if (_health != null)
            {
                _health.OnRevive -= OnRespawn;
                _health.OnDeath -= OnDeath;
                _health.OnHit -= OnHit;
            }
        }
    }
}