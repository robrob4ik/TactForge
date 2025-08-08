using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Run")]
    public class EnigmaCharacterRun : EnigmaCharacterAbility
    {
        public override string HelpBoxText()
        {
            return "This component allows your character to change speed (defined here) when pressing the run button.";
        }

        [Title("Speed")]
        [Tooltip("The speed of the character when it's running")]
        public float RunSpeed = 16f;

        [Title("AutoRun")]
        [Tooltip("Whether or not run should auto trigger if you move the joystick far enough")]
        public bool AutoRun = false;

        /// the input threshold on the joystick (normalized)
        [Tooltip("The input threshold on the joystick (normalized)")]
        public float AutoRunThreshold = 0.6f;

        protected const string _runningAnimationParameterName = "Running";
        protected int _runningAnimationParameter;
        protected bool _runningStarted = false;
        
        protected override void HandleInput()
        {
            if (AutoRun)
            {
                if (_inputManager.PrimaryMovement.magnitude > AutoRunThreshold)
                {
                    _inputManager.RunButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed);
                }
            }

            if (_inputManager.RunButton.IsDown || _inputManager.RunButton.IsPressed)
            {
                RunStart();
            }

            if (_runningStarted)
            {
                if (_inputManager.RunButton.IsUp || _inputManager.RunButton.IsOff)
                {
                    RunStop();
                }
                else
                {
                    if (AutoRun)
                    {
                        if (_inputManager.PrimaryMovement.magnitude <= AutoRunThreshold)
                        {
                            _inputManager.RunButton.State.ChangeState(MMInput.ButtonStates.ButtonUp);
                            RunStop();
                        }
                    }
                }
            }
        }


        /// Every frame we make sure we shouldn't be exiting our run state
        public override void ProcessAbility()
        {
            base.ProcessAbility();
            HandleRunningExit();
        }


        /// Checks if we should exit our running state
        protected virtual void HandleRunningExit()
        {
            if (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal)
            {
                StopAbilityUsedSfx();
            }

            if (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Running && AbilityInProgressSfx != null && _abilityInProgressSfx == null)
            {
                PlayAbilityUsedSfx();
            }

            // if we're running and not grounded, we change our state to Falling
            if (!_controller.Grounded
                && (_condition.CurrentState == EnigmaCharacterStates.CharacterConditions.Normal)
                && (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Running))
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Falling);
                StopFeedbacks();
                StopSfx();
            }

            // if we're not moving fast enough, we go back to idle
            if ((Mathf.Abs(_controller.CurrentMovement.magnitude) < RunSpeed / 10) && (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Running))
            {
                _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                StopFeedbacks();
                StopSfx();
            }

            if (!_controller.Grounded && _abilityInProgressSfx != null)
            {
                StopFeedbacks();
                StopSfx();
            }
        }


        /// Causes the character to start running.
        public virtual void RunStart()
        {
            if (!AbilityAuthorized // if the ability is not permitted
                || (!_controller.Grounded) // or if we're not grounded
                || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal) // or if we're not in normal conditions
                || (_movement.CurrentState != EnigmaCharacterStates.MovementStates.Walking)) // or if we're not walking
            {
                // we do nothing and exit
                return;
            }

            // if the player presses the run button and if we're on the ground and not crouching and we can move freely, 
            // then we change the movement speed in the controller's parameters.
            if (_characterMovement != null)
            {
                _characterMovement.MovementSpeed = RunSpeed;
            }

            // if we're not already running, we trigger our sounds
            if (_movement.CurrentState != EnigmaCharacterStates.MovementStates.Running)
            {
                PlayAbilityStartSfx();
                PlayAbilityUsedSfx();
                PlayAbilityStartFeedbacks();
                _runningStarted = true;
            }

            _movement.ChangeState(EnigmaCharacterStates.MovementStates.Running);
        }


        /// Causes the character to stop running.
        public virtual void RunStop()
        {
            if (_runningStarted)
            {
                // if the run button is released, we revert back to the walking speed.
                if ((_characterMovement != null))
                {
                    _characterMovement.ResetSpeed();
                    _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                }

                StopFeedbacks();
                StopSfx();
                _runningStarted = false;
            }
        }


        /// Stops all run feedbacks
        protected virtual void StopFeedbacks()
        {
            if (_startFeedbackIsPlaying)
            {
                StopStartFeedbacks();
                PlayAbilityStopFeedbacks();
            }
        }


        /// Stops all run sounds
        protected virtual void StopSfx()
        {
            StopAbilityUsedSfx();
            PlayAbilityStopSfx();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (AutoRun)
            {
                RunStop();
                if ((_inputManager != null) && (_inputManager.PrimaryMovement.magnitude > AutoRunThreshold))
                {
                    _inputManager.RunButton.State.ChangeState(MMInput.ButtonStates.Off);
                }
            }
        }


        /// Adds required animator parameters to the animator parameters list if they exist
        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_runningAnimationParameterName, AnimatorControllerParameterType.Bool, out _runningAnimationParameter);
        }


        /// At the end of each cycle, we send our Running status to the character's animator
        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _runningAnimationParameter, (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Running), _character._animatorParameters, _character.RunAnimatorSanityChecks);
        }
    }
}