using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Unity.Entities;

namespace OneBitRob.EnigmaEngine
{
    /// Add this ability to a character, and it'll be able to rotate to face the movement's direction or the weapon's rotation
    [MMHiddenProperties("AbilityStartFeedbacks", "AbilityStopFeedbacks")]
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Orientation 3D")]
    [TemporaryBakingType]
    public class EnigmaCharacterOrientation3D : EnigmaCharacterAbility
    {
        public enum RotationModes
        {
            None,
            MovementDirection,
            WeaponDirection,
            Both
        }

        public enum RotationSpeeds
        {
            Instant,
            Smooth,
            SmoothAbsolute
        }

        [Title("Rotation Mode")]
        [Tooltip("Whether the character should face movement direction, weapon direction, or both, or none")]
        public RotationModes RotationMode = RotationModes.None;

        [Tooltip("If this is false, no rotation will occur")]
        public bool CharacterRotationAuthorized = true;

        [Title("Movement Direction")]
        [Tooltip("If this is true, we'll rotate our model towards the direction")]
        public bool ShouldRotateToFaceMovementDirection = true;

        [MMCondition("ShouldRotateToFaceMovementDirection", true)] [Tooltip("The current rotation mode")]
        public RotationSpeeds MovementRotationSpeed = RotationSpeeds.Instant;

        [MMCondition("ShouldRotateToFaceMovementDirection", true)] [Tooltip("The object we want to rotate towards direction. If left empty, we'll use the Character's model")]
        public GameObject MovementRotatingModel;

        [MMCondition("ShouldRotateToFaceMovementDirection", true)] [Tooltip("The speed at which to rotate towards direction (smooth and absolute only)")]
        public float RotateToFaceMovementDirectionSpeed = 10f;

        [MMCondition("ShouldRotateToFaceMovementDirection", true)] [Tooltip("The threshold after which we start rotating (absolute mode only)")]
        public float AbsoluteThresholdMovement = 0.5f;

        [ReadOnly] 
        [Tooltip("The direction of the model")]
        public Vector3 ModelDirection;

        [ReadOnly] 
        [Tooltip("The direction of the model in angle values")]
        public Vector3 ModelAngles;

        [Title("Weapon Direction")]
        [Tooltip("If this is true, we'll rotate our model towards the weapon's direction")]
        public bool ShouldRotateToFaceWeaponDirection = true;

        [MMCondition("ShouldRotateToFaceWeaponDirection", true)]
        [Tooltip("The current rotation mode")]
        public RotationSpeeds WeaponRotationSpeed = RotationSpeeds.Instant;

        [MMCondition("ShouldRotateToFaceWeaponDirection", true)] 
        [Tooltip("The object we want to rotate towards direction. If left empty, we'll use the Character's model")]
        public GameObject WeaponRotatingModel;

        [MMCondition("ShouldRotateToFaceWeaponDirection", true)] 
        [Tooltip("The speed at which to rotate towards direction (smooth and absolute only)")]
        public float RotateToFaceWeaponDirectionSpeed = 10f;

        [MMCondition("ShouldRotateToFaceWeaponDirection", true)] 
        [Tooltip("The threshold after which we start rotating (absolute mode only)")]
        public float AbsoluteThresholdWeapon = 0.5f;

        [MMCondition("ShouldRotateToFaceWeaponDirection", true)] 
        [Tooltip("The threshold after which we start rotating (absolute mode only)")]
        public bool LockVerticalRotation = true;

        [Title("Animation")]
        [Tooltip("the speed at which the instant rotation animation parameter float resets to 0")]
        public float RotationSpeedResetSpeed = 2f;

        [Tooltip("the speed at which the YRotationOffsetSmoothed should lerp")]
        public float RotationOffsetSmoothSpeed = 1f;

        [Title("Forced Rotation")]
        [Tooltip("Whether the character is being applied a forced rotation")]
        public bool ForcedRotation = false;

        [MMCondition("ForcedRotation", true)] [Tooltip("the forced rotation applied by an external script")]
        public Vector3 ForcedRotationDirection;

        public virtual Vector3 RelativeSpeed
        {
            get { return _relativeSpeed; }
        }

        public virtual Vector3 RelativeSpeedNormalized
        {
            get { return _relativeSpeedNormalized; }
        }

        public virtual float RotationSpeed
        {
            get { return _rotationSpeed; }
        }

        public virtual Vector3 CurrentDirection
        {
            get { return _currentDirection; }
        }

        protected EnigmaCharacterHandleWeapon _characterHandleWeapon;
        protected EnigmaCharacterRun _characterRun;
        protected Vector3 _lastRegisteredVelocity;
        protected Vector3 _rotationDirection;
        protected Vector3 _lastMovement = Vector3.zero;
        protected Vector3 _lastAim = Vector3.zero;
        protected Vector3 _relativeSpeed;
        protected Vector3 _remappedSpeed = Vector3.zero;
        protected Vector3 _relativeMaximum;
        protected Vector3 _relativeSpeedNormalized;
        protected bool _secondaryMovementTriggered = false;
        protected Quaternion _tmpRotation;
        protected Quaternion _newMovementQuaternion;
        protected Quaternion _newWeaponQuaternion;
        protected bool _shouldRotateTowardsWeapon;
        protected float _rotationSpeed;
        protected float _modelAnglesYLastFrame;
        protected float _yRotationOffset;
        protected float _yRotationOffsetSmoothed;
        protected Vector3 _currentDirection;
        protected Vector3 _weaponRotationDirection;
        protected Vector3 _positionLastFrame;
        protected Vector3 _newSpeed;
        protected bool _controllerIsNull;
        protected const string _relativeForwardSpeedAnimationParameterName = "RelativeForwardSpeed";
        protected const string _relativeLateralSpeedAnimationParameterName = "RelativeLateralSpeed";
        protected const string _remappedForwardSpeedAnimationParameterName = "RemappedForwardSpeedNormalized";
        protected const string _remappedLateralSpeedAnimationParameterName = "RemappedLateralSpeedNormalized";
        protected const string _relativeForwardSpeedNormalizedAnimationParameterName = "RelativeForwardSpeedNormalized";
        protected const string _relativeLateralSpeedNormalizedAnimationParameterName = "RelativeLateralSpeedNormalized";
        protected const string _remappedSpeedNormalizedAnimationParameterName = "RemappedSpeedNormalized";
        protected const string _rotationSpeeddAnimationParameterName = "YRotationSpeed";
        protected const string _yRotationOffsetAnimationParameterName = "YRotationOffset";
        protected const string _yRotationOffsetSmoothedAnimationParameterName = "YRotationOffsetSmoothed";
        protected int _relativeForwardSpeedAnimationParameter;
        protected int _relativeLateralSpeedAnimationParameter;
        protected int _remappedForwardSpeedAnimationParameter;
        protected int _remappedLateralSpeedAnimationParameter;
        protected int _relativeForwardSpeedNormalizedAnimationParameter;
        protected int _relativeLateralSpeedNormalizedAnimationParameter;
        protected int _remappedSpeedNormalizedAnimationParameter;
        protected int _rotationSpeeddAnimationParameter;
        protected int _yRotationOffsetAnimationParameter;
        protected int _yRotationOffsetSmoothedAnimationParameter;


        /// On init we grab our model if necessary
        protected override void Initialization()
        {
            base.Initialization();

            if ((_model == null) && (MovementRotatingModel == null) && (WeaponRotatingModel == null))
            {
                Debug.LogError("CharacterOrientation3D on " + this.name +
                               " : you need to set a CharacterModel on your Character component, and/or specify MovementRotatingModel and WeaponRotatingModel on your CharacterOrientation3D inspector. Check the documentation to learn more about this.");
            }

            if (MovementRotatingModel == null)
            {
                MovementRotatingModel = _model;
            }

            _characterRun = _character?.FindAbility<EnigmaCharacterRun>();
            _characterHandleWeapon = _character?.FindAbility<EnigmaCharacterHandleWeapon>();
            if (WeaponRotatingModel == null)
            {
                WeaponRotatingModel = _model;
            }

            _controllerIsNull = _controller == null;
        }


        /// Every frame we rotate towards the direction
        public override void ProcessAbility()
        {
            base.ProcessAbility();

            if (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal)
            {
                return;
            }

            if ((MovementRotatingModel == null) && (WeaponRotatingModel == null))
            {
                return;
            }

            if (!AbilityAuthorized)
            {
                return;
            }

            if (EnigmaGameManager.Instance.Paused)
            {
                return;
            }

            if (CharacterRotationAuthorized)
            {
                RotateToFaceMovementDirection();
                RotateToFaceWeaponDirection();
                RotateModel();
            }
        }


        protected virtual void FixedUpdate()
        {
            ComputeRelativeSpeeds();
        }


        /// Rotates the player model to face the current direction
        protected virtual void RotateToFaceMovementDirection()
        {
            // if we're not supposed to face our direction, we do nothing and exit
            if (!ShouldRotateToFaceMovementDirection)
            {
                return;
            }

            if ((RotationMode != RotationModes.MovementDirection) && (RotationMode != RotationModes.Both))
            {
                return;
            }

            _currentDirection = ForcedRotation || _controllerIsNull ? ForcedRotationDirection : _controller.CurrentDirection;

            // if the rotation mode is instant, we simply rotate to face our direction
            if (MovementRotationSpeed == RotationSpeeds.Instant)
            {
                if (_currentDirection != Vector3.zero)
                {
                    _newMovementQuaternion = Quaternion.LookRotation(_currentDirection);
                }
            }

            // if the rotation mode is smooth, we lerp towards our direction
            if (MovementRotationSpeed == RotationSpeeds.Smooth)
            {
                if (_currentDirection != Vector3.zero)
                {
                    _tmpRotation = Quaternion.LookRotation(_currentDirection);
                    _newMovementQuaternion = Quaternion.Slerp(MovementRotatingModel.transform.rotation, _tmpRotation, Time.deltaTime * RotateToFaceMovementDirectionSpeed);
                }
            }

            // if the rotation mode is smooth, we lerp towards our direction even if the input has been released
            if (MovementRotationSpeed == RotationSpeeds.SmoothAbsolute)
            {
                if (_currentDirection.normalized.magnitude >= AbsoluteThresholdMovement)
                {
                    _lastMovement = _currentDirection;
                }

                if (_lastMovement != Vector3.zero)
                {
                    _tmpRotation = Quaternion.LookRotation(_lastMovement);
                    _newMovementQuaternion = Quaternion.Slerp(MovementRotatingModel.transform.rotation, _tmpRotation, Time.deltaTime * RotateToFaceMovementDirectionSpeed);
                }
            }

            ModelDirection = MovementRotatingModel.transform.forward.normalized;
            ModelAngles = MovementRotatingModel.transform.eulerAngles;
        }


        /// Rotates the character so it faces the weapon's direction
        protected virtual void RotateToFaceWeaponDirection()
        {
            _newWeaponQuaternion = Quaternion.identity;
            _weaponRotationDirection = Vector3.zero;
            _shouldRotateTowardsWeapon = false;

            // if we're not supposed to face our direction, we do nothing and exit
            if (!ShouldRotateToFaceWeaponDirection)
            {
                return;
            }

            if ((RotationMode != RotationModes.WeaponDirection) && (RotationMode != RotationModes.Both))
            {
                return;
            }

            if (_characterHandleWeapon == null)
            {
                return;
            }

            if (_characterHandleWeapon.WeaponAimComponent == null)
            {
                return;
            }

            _shouldRotateTowardsWeapon = true;

            _rotationDirection = _characterHandleWeapon.WeaponAimComponent.CurrentAim.normalized;

            if (LockVerticalRotation)
            {
                _rotationDirection.y = 0;
            }

            _weaponRotationDirection = _rotationDirection;

            MMDebug.DebugDrawArrow(this.transform.position, _rotationDirection, Color.red);

            // if the rotation mode is instant, we simply rotate to face our direction
            if (WeaponRotationSpeed == RotationSpeeds.Instant)
            {
                if (_rotationDirection != Vector3.zero)
                {
                    _newWeaponQuaternion = Quaternion.LookRotation(_rotationDirection);
                }
            }

            // if the rotation mode is smooth, we lerp towards our direction
            if (WeaponRotationSpeed == RotationSpeeds.Smooth)
            {
                if (_rotationDirection != Vector3.zero)
                {
                    _tmpRotation = Quaternion.LookRotation(_rotationDirection);
                    _newWeaponQuaternion = Quaternion.Slerp(WeaponRotatingModel.transform.rotation, _tmpRotation, Time.deltaTime * RotateToFaceWeaponDirectionSpeed);
                }
            }

            // if the rotation mode is smooth, we lerp towards our direction even if the input has been released
            if (WeaponRotationSpeed == RotationSpeeds.SmoothAbsolute)
            {
                if (_rotationDirection.normalized.magnitude >= AbsoluteThresholdWeapon)
                {
                    _lastMovement = _rotationDirection;
                }

                if (_lastMovement != Vector3.zero)
                {
                    _tmpRotation = Quaternion.LookRotation(_lastMovement);
                    _newWeaponQuaternion = Quaternion.Slerp(WeaponRotatingModel.transform.rotation, _tmpRotation, Time.deltaTime * RotateToFaceWeaponDirectionSpeed);
                }
            }
        }


        /// Rotates models if needed
        protected virtual void RotateModel()
        {
            MovementRotatingModel.transform.rotation = _newMovementQuaternion;

            if (_shouldRotateTowardsWeapon && (_weaponRotationDirection != Vector3.zero))
            {
                WeaponRotatingModel.transform.rotation = _newWeaponQuaternion;
            }
        }


        /// Computes the relative speeds
        protected virtual void ComputeRelativeSpeeds()
        {
            if ((MovementRotatingModel == null) && (WeaponRotatingModel == null))
            {
                return;
            }

            if (Time.deltaTime != 0f)
            {
                _newSpeed = (this.transform.position - _positionLastFrame) / Time.deltaTime;
            }

            // relative speed
            if ((_characterHandleWeapon == null) || (_characterHandleWeapon.CurrentWeapon == null))
            {
                _relativeSpeed = MovementRotatingModel.transform.InverseTransformVector(_newSpeed);
            }
            else
            {
                _relativeSpeed = WeaponRotatingModel.transform.InverseTransformVector(_newSpeed);
            }

            // remapped speed

            float maxSpeed = 0f;
            if (_characterMovement != null)
            {
                maxSpeed = _characterMovement.WalkSpeed;
            }

            if (_characterRun != null)
            {
                maxSpeed = _characterRun.RunSpeed;
            }

            _relativeMaximum = _character.transform.TransformVector(Vector3.one);

            _remappedSpeed.x = MMMaths.Remap(_relativeSpeed.x, 0f, maxSpeed, 0f, _relativeMaximum.x);
            _remappedSpeed.y = MMMaths.Remap(_relativeSpeed.y, 0f, maxSpeed, 0f, _relativeMaximum.y);
            _remappedSpeed.z = MMMaths.Remap(_relativeSpeed.z, 0f, maxSpeed, 0f, _relativeMaximum.z);

            // relative speed normalized
            _relativeSpeedNormalized = _relativeSpeed.normalized;
            _yRotationOffset = _modelAnglesYLastFrame - ModelAngles.y;

            _yRotationOffsetSmoothed = Mathf.Lerp(_yRotationOffsetSmoothed, _yRotationOffset, RotationOffsetSmoothSpeed * Time.deltaTime);

            // RotationSpeed
            if (Mathf.Abs(_modelAnglesYLastFrame - ModelAngles.y) > 1f)
            {
                _rotationSpeed = Mathf.Abs(_modelAnglesYLastFrame - ModelAngles.y);
            }
            else
            {
                _rotationSpeed -= Time.time * RotationSpeedResetSpeed;
            }

            if (_rotationSpeed <= 0f)
            {
                _rotationSpeed = 0f;
            }

            _modelAnglesYLastFrame = ModelAngles.y;
            _positionLastFrame = this.transform.position;
        }


        /// Forces the character's model to face in the specified direction
        /// <param name="direction"></param>
        public virtual void Face(EnigmaCharacter.FacingDirections direction)
        {
            switch (direction)
            {
                case EnigmaCharacter.FacingDirections.East:
                    _newMovementQuaternion = Quaternion.LookRotation(Vector3.right);
                    break;
                case EnigmaCharacter.FacingDirections.North:
                    _newMovementQuaternion = Quaternion.LookRotation(Vector3.forward);
                    break;
                case EnigmaCharacter.FacingDirections.South:
                    _newMovementQuaternion = Quaternion.LookRotation(Vector3.back);
                    break;
                case EnigmaCharacter.FacingDirections.West:
                    _newMovementQuaternion = Quaternion.LookRotation(Vector3.left);
                    break;
            }
        }


        /// Forces the character's model to face the specified angles
        /// <param name="angles"></param>
        public virtual void Face(Vector3 angles)
        {
            _newMovementQuaternion = Quaternion.LookRotation(Quaternion.Euler(angles) * Vector3.forward);
        }


        /// Adds required animator parameters to the animator parameters list if they exist
        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_rotationSpeeddAnimationParameterName, AnimatorControllerParameterType.Float, out _rotationSpeeddAnimationParameter);
            RegisterAnimatorParameter(_relativeForwardSpeedAnimationParameterName, AnimatorControllerParameterType.Float, out _relativeForwardSpeedAnimationParameter);
            RegisterAnimatorParameter(_relativeLateralSpeedAnimationParameterName, AnimatorControllerParameterType.Float, out _relativeLateralSpeedAnimationParameter);
            RegisterAnimatorParameter(_remappedForwardSpeedAnimationParameterName, AnimatorControllerParameterType.Float, out _remappedForwardSpeedAnimationParameter);
            RegisterAnimatorParameter(_remappedLateralSpeedAnimationParameterName, AnimatorControllerParameterType.Float, out _remappedLateralSpeedAnimationParameter);
            RegisterAnimatorParameter(_relativeForwardSpeedNormalizedAnimationParameterName, AnimatorControllerParameterType.Float, out _relativeForwardSpeedNormalizedAnimationParameter);
            RegisterAnimatorParameter(_relativeLateralSpeedNormalizedAnimationParameterName, AnimatorControllerParameterType.Float, out _relativeLateralSpeedNormalizedAnimationParameter);
            RegisterAnimatorParameter(_remappedSpeedNormalizedAnimationParameterName, AnimatorControllerParameterType.Float, out _remappedSpeedNormalizedAnimationParameter);
            RegisterAnimatorParameter(_yRotationOffsetAnimationParameterName, AnimatorControllerParameterType.Float, out _yRotationOffsetAnimationParameter);
            RegisterAnimatorParameter(_yRotationOffsetSmoothedAnimationParameterName, AnimatorControllerParameterType.Float, out _yRotationOffsetSmoothedAnimationParameter);
        }


        /// Sends the current speed and the current value of the Walking state to the animator
        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _rotationSpeeddAnimationParameter, _rotationSpeed, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _relativeForwardSpeedAnimationParameter, _relativeSpeed.z, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _relativeLateralSpeedAnimationParameter, _relativeSpeed.x, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _remappedForwardSpeedAnimationParameter, _remappedSpeed.z, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _remappedLateralSpeedAnimationParameter, _remappedSpeed.x, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _relativeForwardSpeedNormalizedAnimationParameter, _relativeSpeedNormalized.z, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _relativeLateralSpeedNormalizedAnimationParameter, _relativeSpeedNormalized.x, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _remappedSpeedNormalizedAnimationParameter, _remappedSpeed.magnitude, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _yRotationOffsetAnimationParameter, _yRotationOffset, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorFloat(_animator, _yRotationOffsetSmoothedAnimationParameter, _yRotationOffsetSmoothed, _character._animatorParameters, _character.RunAnimatorSanityChecks);
        }
    }
}