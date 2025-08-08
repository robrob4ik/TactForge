using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Unity.Entities;

namespace OneBitRob.EnigmaEngine
{
    /// Add this ability to a Character to have it handle ground movement (walk, and potentially run, crawl, etc) in x and z direction for 3D, x and y for 2D
    /// Animator parameters : Speed (float), Walking (bool)
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Movement")]
    [TemporaryBakingType]
    public class EnigmaCharacterMovement : EnigmaCharacterAbility
    {
        /// the possible rotation modes for the character
        public enum Movements
        {
            Free,
            Strict4Directions,
            Strict8Directions
        }

        public virtual float MovementSpeed { get; set; }

        public virtual bool MovementForbidden { get; set; }

        [Title("Direction")] 
        [Tooltip("Whether the character can move freely, in 2D only, in 4 or 8 cardinal directions")]
        public Movements Movement = Movements.Free;

        [Title("Settings")] 
        [Tooltip("Whether or not movement input is authorized at that time")]
        public bool InputAuthorized = true;

        [Tooltip("Whether or not input should be analog")]
        public bool AnalogInput = false;

        [Tooltip("Whether or not input should be set from another script")]
        public bool ScriptDrivenInput = false;

        [Title("Speed")] 
        [Tooltip("The speed of the character when it's walking")]
        public float WalkSpeed = 4f;

        [Tooltip("Whether or not this component should set the controller's movement")]
        public bool ShouldSetMovement = true;

        [Tooltip("The speed threshold after which the character is not considered idle anymore")]
        public float IdleThreshold = 0.05f;

        [Title("Acceleration")] 
        [Tooltip("The acceleration to apply to the current speed / 0f : no acceleration, instant full speed")]
        public float Acceleration = 10f;

        [Tooltip("The deceleration to apply to the current speed / 0f : no deceleration, instant stop")]
        public float Deceleration = 10f;

        [Tooltip("Whether or not to interpolate movement speed")]
        public bool InterpolateMovementSpeed = false;

        public virtual float MovementSpeedMaxMultiplier { get; set; } = float.MaxValue;
        private float _movementSpeedMultiplier;
        
        public float MovementSpeedMultiplier
        {
            get => Mathf.Min(_movementSpeedMultiplier, MovementSpeedMaxMultiplier);
            set => _movementSpeedMultiplier = value;
        }

        public Stack<float> ContextSpeedStack = new Stack<float>();

        public virtual float ContextSpeedMultiplier => ContextSpeedStack.Count > 0 ? ContextSpeedStack.Peek() : 1;

        [FoldoutGroup("Feedbacks", expanded: false)]
        [Title("Walk Feedback")] 
        [Tooltip("The particles to trigger while walking")]
        public ParticleSystem[] WalkParticles;

        [FoldoutGroup("Feedbacks")]
        [Title("Touch The Ground Feedback")] 
        [Tooltip("The particles to trigger when touching the ground")]
        public ParticleSystem[] TouchTheGroundParticles;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The sfx to trigger when touching the ground")]
        public AudioClip[] TouchTheGroundSfx;

        protected float _movementSpeed;
        protected float _horizontalMovement;
        protected float _verticalMovement;
        protected Vector3 _movementVector;
        protected Vector2 _currentInput = Vector2.zero;
        protected Vector2 _normalizedInput;
        protected Vector2 _lerpedInput = Vector2.zero;
        protected float _acceleration = 0f;
        protected bool _walkParticlesPlaying = false;

        protected const string _speedAnimationParameterName = "Speed";
        protected const string _walkingAnimationParameterName = "Walking";
        protected const string _idleAnimationParameterName = "Idle";
        protected int _speedAnimationParameter;
        protected int _walkingAnimationParameter;
        protected int _idleAnimationParameter;


        /// On Initialization, we set our movement speed to WalkSpeed.
        protected override void Initialization()
        {
            base.Initialization();
            ResetAbility();
        }


        /// Resets character movement states and speeds
        public override void ResetAbility()
        {
            base.ResetAbility();
            MovementSpeed = WalkSpeed;
            if (ContextSpeedStack != null)
            {
                ContextSpeedStack.Clear();
            }

            if ((_movement != null) && (_movement.CurrentState != EnigmaCharacterStates.MovementStates.FallingDownHole))
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
            }

            MovementSpeedMultiplier = 1f;
            MovementForbidden = false;

            foreach (ParticleSystem system in TouchTheGroundParticles)
            {
                if (system != null)
                {
                    system.Stop();
                }
            }

            foreach (ParticleSystem system in WalkParticles)
            {
                if (system != null)
                {
                    system.Stop();
                }
            }
        }
        
        public override void ProcessAbility()
        {
            base.ProcessAbility();

            HandleFrozen();

            if (!AbilityAuthorized
                || ((_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal) &&
                    (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.ControlledMovement)))
            {
                if (AbilityAuthorized)
                {
                    StopAbilityUsedSfx();
                }

                return;
            }

            HandleDirection();
            HandleMovement();
            Feedbacks();
        }
        
        protected override void HandleInput()
        {
            if (ScriptDrivenInput)
            {
                return;
            }

            if (InputAuthorized)
            {
                _horizontalMovement = _horizontalInput;
                _verticalMovement = _verticalInput;
            }
            else
            {
                _horizontalMovement = 0f;
                _verticalMovement = 0f;
            }
        }
        
        public virtual void SetMovement(Vector2 value)
        {
            _horizontalMovement = value.x;
            _verticalMovement = value.y;
        }
        
        public virtual void SetHorizontalMovement(float value)
        {
            _horizontalMovement = value;
        }
        
        public virtual void SetVerticalMovement(float value)
        {
            _verticalMovement = value;
        }
        
        public virtual void ApplyMovementMultiplier(float movementMultiplier, float duration)
        {
            StartCoroutine(ApplyMovementMultiplierCo(movementMultiplier, duration));
        }
        
        protected virtual IEnumerator ApplyMovementMultiplierCo(float movementMultiplier, float duration)
        {
            if (_characterMovement == null)
            {
                yield break;
            }

            SetContextSpeedMultiplier(movementMultiplier);
            yield return MMCoroutine.WaitFor(duration);
            ResetContextSpeedMultiplier();
        }
        
        public virtual void SetContextSpeedMultiplier(float newMovementSpeedMultiplier)
        {
            ContextSpeedStack.Push(newMovementSpeedMultiplier);
        }
        
        public virtual void ResetContextSpeedMultiplier()
        {
            if (ContextSpeedStack.Count <= 0)
            {
                return;
            }

            ContextSpeedStack.Pop();
        }
        
        protected virtual void HandleDirection()
        {
            switch (Movement)
            {
                case Movements.Free:
                    break;
                case Movements.Strict4Directions:
                    if (Mathf.Abs(_horizontalMovement) > Mathf.Abs(_verticalMovement))
                    {
                        _verticalMovement = 0f;
                    }
                    else
                    {
                        _horizontalMovement = 0f;
                    }

                    break;
                case Movements.Strict8Directions:
                    _verticalMovement = Mathf.Round(_verticalMovement);
                    _horizontalMovement = Mathf.Round(_horizontalMovement);
                    break;
            }
        }
        
        protected virtual void HandleMovement()
        {
            if ((_movement.CurrentState != EnigmaCharacterStates.MovementStates.Walking) && _startFeedbackIsPlaying)
            {
                StopStartFeedbacks();
            }

            if (_movement.CurrentState != EnigmaCharacterStates.MovementStates.Walking && _abilityInProgressSfx != null)
            {
                StopAbilityUsedSfx();
            }

            if (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Walking && _abilityInProgressSfx == null)
            {
                PlayAbilityUsedSfx();
            }

            if (!AbilityAuthorized || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal))
            {
                return;
            }

            CheckJustGotGrounded();

            if (MovementForbidden)
            {
                _horizontalMovement = 0f;
                _verticalMovement = 0f;
            }

            if (!_controller.Grounded
                && (_condition.CurrentState == EnigmaCharacterStates.CharacterConditions.Normal)
                && (
                    (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Walking)
                    || (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Idle)
                ))
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Falling);
            }

            if (_controller.Grounded && (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Falling))
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
            }

            if (_controller.Grounded
                && (_controller.CurrentMovement.magnitude > IdleThreshold)
                && (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Idle))
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Walking);
                PlayAbilityStartSfx();
                PlayAbilityUsedSfx();
                PlayAbilityStartFeedbacks();
            }

            if ((_movement.CurrentState == EnigmaCharacterStates.MovementStates.Walking)
                // TODO Probably magnitude simplification
                && (_controller.CurrentMovement.magnitude <= IdleThreshold))
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                PlayAbilityStopSfx();
                PlayAbilityStopFeedbacks();
            }

            if (ShouldSetMovement)
            {
                SetMovement();
            }
        }


        /// Describes what happens when the character is in the frozen state
        protected virtual void HandleFrozen()
        {
            if (!AbilityAuthorized)
            {
                return;
            }

            if (_condition.CurrentState == EnigmaCharacterStates.CharacterConditions.Frozen)
            {
                _horizontalMovement = 0f;
                _verticalMovement = 0f;
                SetMovement();
            }
        }
        
        protected virtual void SetMovement()
        {
            _movementVector = Vector3.zero;
            _currentInput = Vector2.zero;

            _currentInput.x = _horizontalMovement;
            _currentInput.y = _verticalMovement;

            _normalizedInput = _currentInput.normalized;

            float interpolationSpeed = 1f;

            if ((Acceleration == 0) || (Deceleration == 0))
            {
                _lerpedInput = AnalogInput ? _currentInput : _normalizedInput;
            }
            else
            {
                if (_normalizedInput.magnitude == 0)
                {
                    _acceleration = Mathf.Lerp(_acceleration, 0f, Deceleration * Time.deltaTime);
                    _lerpedInput = Vector2.Lerp(_lerpedInput, _lerpedInput * _acceleration,
                        Time.deltaTime * Deceleration);
                    interpolationSpeed = Deceleration;
                }
                else
                {
                    _acceleration = Mathf.Lerp(_acceleration, 1f, Acceleration * Time.deltaTime);
                    _lerpedInput = AnalogInput
                        ? Vector2.ClampMagnitude(_currentInput, _acceleration)
                        : Vector2.ClampMagnitude(_normalizedInput, _acceleration);
                    interpolationSpeed = Acceleration;
                }
            }

            _movementVector.x = _lerpedInput.x;
            _movementVector.y = 0f;
            _movementVector.z = _lerpedInput.y;

            if (InterpolateMovementSpeed)
            {
                _movementSpeed = Mathf.Lerp(_movementSpeed,
                    MovementSpeed * ContextSpeedMultiplier * MovementSpeedMultiplier,
                    interpolationSpeed * Time.deltaTime);
            }
            else
            {
                _movementSpeed = MovementSpeed * MovementSpeedMultiplier * ContextSpeedMultiplier;
            }

            _movementVector *= _movementSpeed;

            if (_movementVector.magnitude > MovementSpeed * ContextSpeedMultiplier * MovementSpeedMultiplier)
            {
                _movementVector = Vector3.ClampMagnitude(_movementVector, MovementSpeed);
            }

            if ((_currentInput.magnitude <= IdleThreshold) && (_controller.CurrentMovement.magnitude < IdleThreshold))
            {
                _movementVector = Vector3.zero;
            }

            _controller.SetMovement(_movementVector);
        }


        /// Every frame, checks if we just hit the ground, and if yes, changes the state and triggers a particle effect
        protected virtual void CheckJustGotGrounded()
        {
            // if the character just got grounded
            if (_controller.JustGotGrounded)
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
            }
        }


        /// Plays particles when walking, and particles and sounds when landing
        protected virtual void Feedbacks()
        {
            if (_controller.Grounded)
            {
                if (_controller.CurrentMovement.magnitude > IdleThreshold)
                {
                    foreach (ParticleSystem system in WalkParticles)
                    {
                        if (!_walkParticlesPlaying && (system != null))
                        {
                            system.Play();
                        }

                        _walkParticlesPlaying = true;
                    }
                }
                else
                {
                    foreach (ParticleSystem system in WalkParticles)
                    {
                        if (_walkParticlesPlaying && (system != null))
                        {
                            system.Stop();
                            _walkParticlesPlaying = false;
                        }
                    }
                }
            }
            else
            {
                foreach (ParticleSystem system in WalkParticles)
                {
                    if (_walkParticlesPlaying && (system != null))
                    {
                        system.Stop();
                        _walkParticlesPlaying = false;
                    }
                }
            }

            if (_controller.JustGotGrounded)
            {
                foreach (ParticleSystem system in TouchTheGroundParticles)
                {
                    if (system != null)
                    {
                        system.Clear();
                        system.Play();
                    }
                }

                foreach (AudioClip clip in TouchTheGroundSfx)
                {
                    MMSoundManagerSoundPlayEvent.Trigger(clip, MMSoundManager.MMSoundManagerTracks.Sfx,
                        this.transform.position);
                }
            }
        }


        /// Resets this character's speed
        public virtual void ResetSpeed()
        {
            MovementSpeed = WalkSpeed;
        }


        /// On Respawn, resets the speed
        protected override void OnRespawn()
        {
            ResetSpeed();
            MovementForbidden = false;
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            DisableWalkParticles();
        }


        /// Disables all walk particle systems that may be playing
        protected virtual void DisableWalkParticles()
        {
            if (WalkParticles.Length > 0)
            {
                foreach (ParticleSystem walkParticle in WalkParticles)
                {
                    if (walkParticle != null)
                    {
                        walkParticle.Stop();
                    }
                }
            }
        }


        /// On disable we make sure to turn off anything that could still be playing
        protected override void OnDisable()
        {
            base.OnDisable();
            DisableWalkParticles();
            PlayAbilityStopSfx();
            PlayAbilityStopFeedbacks();
            StopAbilityUsedSfx();
        }


        /// Adds required animator parameters to the animator parameters list if they exist
        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_speedAnimationParameterName, AnimatorControllerParameterType.Float,
                out _speedAnimationParameter);
            RegisterAnimatorParameter(_walkingAnimationParameterName, AnimatorControllerParameterType.Bool,
                out _walkingAnimationParameter);
            RegisterAnimatorParameter(_idleAnimationParameterName, AnimatorControllerParameterType.Bool,
                out _idleAnimationParameter);
        }


        /// Sends the current speed and the current value of the Walking state to the animator
        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _speedAnimationParameter,
                Mathf.Abs(_controller.CurrentMovement.magnitude), _character._animatorParameters,
                _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _walkingAnimationParameter,
                (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Walking),
                _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _idleAnimationParameter,
                (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Idle), _character._animatorParameters,
                _character.RunAnimatorSanityChecks);
        }
    }
}