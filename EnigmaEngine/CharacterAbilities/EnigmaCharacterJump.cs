using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
	[AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Jump")]
	public class EnigmaCharacterJump : EnigmaCharacterAbility 
	{
		[Title("Jump Settings")]
		[Tooltip("Whether or not the jump should be proportional to press (if yes, releasing the button will stop the jump)")]
		public bool JumpProportionalToPress = true;
	
		[Tooltip("The minimum amount of time after the jump's start before releasing the jump button has any effect")]
		public float MinimumPressTime = 0.4f;
		
		[Tooltip("The force to apply to the jump, the higher the jump, the faster the jump")]
		public float JumpForce = 800f;
		
		[Tooltip("the height the jump should have")]
		public float JumpHeight = 4f;

		[Title("Slopes")]
		[Tooltip("Whether or not the character can jump if standing on a slope too steep to walk on")]
		public bool CanJumpOnTooSteepSlopes = true;
		[Tooltip("Whether or not standing on a slope too steep to walk on should reset jump counters")]
		public bool ResetJumpsOnTooSteepSlopes = false;
        
		[Title("Number of Jumps")]
		[Tooltip("the maximum number of jumps allowed (0 : no jump, 1 : normal jump, 2 : double jump, etc...)")]
		public int NumberOfJumps = 1;

		[MMReadOnly]
		[Tooltip("the number of jumps left to the character")]
		public int NumberOfJumpsLeft = 0;

		[Title("Feedbacks")]
		[Tooltip("the feedback to play when the jump starts")]
		public MMFeedbacks JumpStartFeedback;
		
		[Tooltip("the feedback to play when the jump stops")]
		public MMFeedbacks JumpStopFeedback;

		protected bool _doubleJumping;
		protected Vector3 _jumpForce;
		protected Vector3 _jumpOrigin;
		protected bool _jumpStopped = false;
		protected float _jumpStartedAt = 0f;
		protected bool _buttonReleased = false;
		protected int _initialNumberOfJumps;

		protected const string _jumpingAnimationParameterName = "Jumping";
		protected const string _doubleJumpingAnimationParameterName = "DoubleJumping";
		protected const string _hitTheGroundAnimationParameterName = "HitTheGround";
		protected int _jumpingAnimationParameter;
		protected int _doubleJumpingAnimationParameter;
		protected int _hitTheGroundAnimationParameter;

		protected override void Initialization()
		{
			base.Initialization ();
			ResetNumberOfJumps();
			_jumpStopped = true;
			JumpStartFeedback?.Initialization(this.gameObject);
			JumpStopFeedback?.Initialization(this.gameObject);
			_initialNumberOfJumps = NumberOfJumps;
		}

		protected override void HandleInput()
		{
			base.HandleInput();

			if (!AbilityAuthorized
			    || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal))
			{
				return;
			}
			if (_inputManager.JumpButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
			{
				JumpStart();
			}
			if (_inputManager.JumpButton.State.CurrentState == MMInput.ButtonStates.ButtonUp)
			{               
				_buttonReleased = true;                               
			}
		}

		public override void ProcessAbility()
		{
			if (_controller.JustGotGrounded)
			{
				ResetNumberOfJumps();
			}

			// if movement is prevented, or if the character is dead/frozen/can't move, we exit and do nothing
			if (!AbilityAuthorized
			    || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal))
			{
				return;
			}

			if (!_jumpStopped
			    &&
			    ((_movement.CurrentState == EnigmaCharacterStates.MovementStates.Idle)
			     || (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Walking)
			     || (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Running)
			     || (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Crouching)
			     || (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Crawling)
			     || (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Pushing)
			     || (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Falling)
			    ))
			{
				JumpStop();
			}

			if (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Jumping)
			{
				if (_buttonReleased 
				    && !_jumpStopped
				    && JumpProportionalToPress 
				    && (Time.time - _jumpStartedAt > MinimumPressTime))
				{
					JumpStop();
				}
	            
				if (!_jumpStopped)
				{
					if ((this.transform.position.y - _jumpOrigin.y > JumpHeight)
					    || CeilingTest())
					{
						JumpStop();
						_controller3D.Grounded = _controller3D.IsGroundedTest();
						if (_controller.Grounded)
						{
							ResetNumberOfJumps();  
						}
					}
					else
					{
						_jumpForce = Vector3.up * JumpForce * Time.deltaTime;
						_controller.AddForce(_jumpForce);
					}
				}
			}

		}

		protected virtual bool CeilingTest()
		{
			bool returnValue = _controller3D.CollidingAbove();
			return returnValue;
		}

		public virtual void JumpStart()
		{
			if (!EvaluateJumpConditions())
			{
				return;
			}

			if (NumberOfJumpsLeft != NumberOfJumps)
			{
				_doubleJumping = true;
			}
			
			// we decrease the number of jumps left
			NumberOfJumpsLeft = NumberOfJumpsLeft - 1;

			_movement.ChangeState(EnigmaCharacterStates.MovementStates.Jumping);	
			EnigmaCharacterEvent.Trigger(_character, EnigmaCharacterEventTypes.Jump);
			JumpStartFeedback?.PlayFeedbacks(this.transform.position);
			_jumpOrigin = this.transform.position;
			_jumpStopped = false;
			_jumpStartedAt = Time.time;
			_controller.Grounded = false;
			_controller.GravityActive = false;
			_buttonReleased = false;

			PlayAbilityStartSfx();
			PlayAbilityUsedSfx();
			PlayAbilityStartFeedbacks();
		}

		public virtual void JumpStop()
		{
			_controller.GravityActive = true;
			if (_controller.Velocity.y > 0)
			{
				_controller.Velocity.y = 0f;
			}
			_jumpStopped = true;
			_buttonReleased = false;
			PlayAbilityStopSfx();
			StopAbilityUsedSfx();
			StopStartFeedbacks();
			PlayAbilityStopFeedbacks();
			JumpStopFeedback?.PlayFeedbacks(this.transform.position);
		}

		public virtual void ResetNumberOfJumps()
		{
			bool shouldResetJumps = true;

			if (shouldResetJumps)
			{
				NumberOfJumpsLeft = NumberOfJumps;
			}
			
			_doubleJumping = false;
		}

		protected virtual bool EvaluateJumpConditions()
		{
			if (!AbilityAuthorized)
			{
				return false;
			}
			
			if (CeilingTest())
			{
				return false;
			}

			if (NumberOfJumpsLeft <= 0)
			{
				return false;
			}

			if (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Dashing)
			{
				return false;
			}
			return true;
		}

		protected override void InitializeAnimatorParameters()
		{
			RegisterAnimatorParameter (_jumpingAnimationParameterName, AnimatorControllerParameterType.Bool, out _jumpingAnimationParameter);
			RegisterAnimatorParameter (_doubleJumpingAnimationParameterName, AnimatorControllerParameterType.Bool, out _doubleJumpingAnimationParameter);
			RegisterAnimatorParameter (_hitTheGroundAnimationParameterName, AnimatorControllerParameterType.Bool, out _hitTheGroundAnimationParameter);
		}

	
		public override void UpdateAnimator()
		{
			MMAnimatorExtensions.UpdateAnimatorBool(_animator, _jumpingAnimationParameter, (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Jumping),_character._animatorParameters, _character.RunAnimatorSanityChecks);
			MMAnimatorExtensions.UpdateAnimatorBool(_animator, _doubleJumpingAnimationParameter, _doubleJumping,_character._animatorParameters, _character.RunAnimatorSanityChecks);
			MMAnimatorExtensions.UpdateAnimatorBool (_animator, _hitTheGroundAnimationParameter, _controller.JustGotGrounded, _character._animatorParameters, _character.RunAnimatorSanityChecks);
		}
	}
}