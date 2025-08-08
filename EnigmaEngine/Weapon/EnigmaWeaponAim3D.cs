using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace OneBitRob.EnigmaEngine
{
    [RequireComponent(typeof(EnigmaWeapon))]
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Weapon Aim 3D")]
    public class EnigmaWeaponAim3D : EnigmaWeaponAim
    {
        public enum AimCenters
        {
            Owner,
            Weapon
        }

        [FoldoutGroup("3D"), Title("3D Settings")]
        [Tooltip("If this is true, aim will be unrestricted to angles, and will aim freely in all 3 axis, useful when dealing with AI and elevation")]
        public bool Unrestricted3DAim = false;

        [FoldoutGroup("3D")]
        [Tooltip("Whether aim direction should be computed from the owner, or from the weapon")]
        public AimCenters AimCenter = AimCenters.Owner;

        [FoldoutGroup("Reticle and slopes"), Title("Reticle and Slopes")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("Whether or not the reticle should move vertically to stay above slopes")]
        public bool ReticleMovesWithSlopes = false;

        [FoldoutGroup("Reticle and slopes")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("The layers the reticle should consider as obstacles")]
        public LayerMask ReticleObstacleMask = EnigmaLayerManager.ObstaclesLayerMask;

        [FoldoutGroup("Reticle and slopes")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("The maximum slope elevation for the reticle")]
        public float MaximumSlopeElevation = 50f;

        [FoldoutGroup("3D")]
        [Tooltip("If this is true, the aim system will try to compensate when aim direction is null (for example when you haven't set any primary input yet)")]
        public bool AvoidNullAim = true;

        protected Vector2 _inputMovement;
        protected Vector3 _slopeTargetPosition;
        protected Vector3 _weaponAimCurrentAim;

        protected override void Initialization()
        {
            if (_initialized)
            {
                return;
            }

            base.Initialization();
            _mainCamera = Camera.main;
        }

        protected virtual void Reset()
        {
            ReticleObstacleMask = LayerMask.NameToLayer("Ground");
        }


        /// Computes the current aim direction
        protected override void GetCurrentAim()
        {
            if (!AimControlActive)
            {
                if (ReticleType == ReticleTypes.Scene)
                {
                    ComputeReticlePosition();
                }

                return;
            }

            if (EnigmaWeapon.Owner == null)
            {
                return;
            }

            if ((EnigmaWeapon.Owner.LinkedInputManager == null) && (EnigmaWeapon.Owner.CharacterType == EnigmaCharacter.CharacterTypes.Player))
            {
                return;
            }

            if ((EnigmaWeapon.Owner.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal) &&
                (EnigmaWeapon.Owner.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.ControlledMovement))
            {
                return;
            }

            AutoDetectWeaponMode();

            switch (AimControl)
            {
                case AimControls.Off:
                    if (EnigmaWeapon.Owner == null)
                    {
                        return;
                    }

                    GetOffAim();
                    break;

                case AimControls.Script:
                    GetScriptAim();
                    break;

                case AimControls.PrimaryMovement:
                    if ((EnigmaWeapon.Owner == null) || (EnigmaWeapon.Owner.LinkedInputManager == null))
                    {
                        return;
                    }

                    GetPrimaryMovementAim();
                    break;

                case AimControls.SecondaryMovement:
                    if ((EnigmaWeapon.Owner == null) || (EnigmaWeapon.Owner.LinkedInputManager == null))
                    {
                        return;
                    }

                    GetSecondaryMovementAim();
                    break;
                

                case AimControls.Mouse:
                    if (EnigmaWeapon.Owner == null)
                    {
                        return;
                    }

                    GetMouseAim();
                    break;
                
            }

            if (AvoidNullAim && (_currentAim == Vector3.zero))
            {
                GetOffAim();
            }
        }

        public virtual void GetOffAim()
        {
            _currentAim = Vector3.right;
            _weaponAimCurrentAim = _currentAim;
            _direction = Vector3.right;
        }

        public virtual void GetPrimaryMovementAim()
        {
            if (_lastNonNullMovement == Vector2.zero)
            {
                _lastNonNullMovement = EnigmaWeapon.Owner.LinkedInputManager.LastNonNullPrimaryMovement;
            }

            _inputMovement = EnigmaWeapon.Owner.LinkedInputManager.PrimaryMovement;
            _inputMovement = _inputMovement.magnitude > MinimumMagnitude ? _inputMovement : _lastNonNullMovement;

            _currentAim.x = _inputMovement.x;
            _currentAim.y = 0f;
            _currentAim.z = _inputMovement.y;
            _weaponAimCurrentAim = _currentAim;
            _direction = transform.position + _currentAim;

            _lastNonNullMovement = _inputMovement.magnitude > MinimumMagnitude ? _inputMovement : _lastNonNullMovement;
        }

        public virtual void GetSecondaryMovementAim()
        {
            if (_lastNonNullMovement == Vector2.zero)
            {
                _lastNonNullMovement = EnigmaWeapon.Owner.LinkedInputManager.LastNonNullSecondaryMovement;
            }

            _inputMovement = EnigmaWeapon.Owner.LinkedInputManager.SecondaryMovement;
            _inputMovement = _inputMovement.magnitude > MinimumMagnitude ? _inputMovement : _lastNonNullMovement;

            _currentAim.x = _inputMovement.x;
            _currentAim.y = 0f;
            _currentAim.z = _inputMovement.y;
            _weaponAimCurrentAim = _currentAim;
            _direction = transform.position + _currentAim;

            _lastNonNullMovement = _inputMovement.magnitude > MinimumMagnitude ? _inputMovement : _lastNonNullMovement;
        }

        public virtual void GetScriptAim()
        {
            _direction = -(transform.position - _currentAim);
            _weaponAimCurrentAim = _currentAim;
        }

        public virtual void GetMouseAim()
        {
            ComputeReticlePosition();

            if (Vector3.Distance(_direction, transform.position) < MouseDeadZoneRadius)
            {
                _direction = _lastMousePosition;
            }
            else
            {
                _lastMousePosition = _direction;
            }

            _direction.y = transform.position.y;
            _currentAim = _direction - EnigmaWeapon.Owner.transform.position;

            if (AimCenter == AimCenters.Owner)
            {
                _weaponAimCurrentAim = _direction - EnigmaWeapon.Owner.transform.position;
            }
            else
            {
                _weaponAimCurrentAim = _direction - EnigmaWeapon.transform.position;
                if (EnigmaWeapon.WeaponUseTransform)
                {
                    _weaponAimCurrentAim = _direction - EnigmaWeapon.WeaponUseTransform.position;
                }
            }
        }

        protected virtual void ComputeReticlePosition()
        {
            _mousePosition = EnigmaInputManager.Instance.MousePosition;

            Ray ray = _mainCamera.ScreenPointToRay(_mousePosition);
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.yellow);
            float distance;
            if (_playerPlane.Raycast(ray, out distance))
            {
                Vector3 target = ray.GetPoint(distance);
                _direction = target;
            }

            _reticlePosition = _direction;
        }


        /// Every frame, we compute the aim direction and rotate the weapon accordingly
        protected override void Update()
        {
            HideMousePointer();
            HideReticle();
            if (EnigmaGameManager.HasInstance && EnigmaGameManager.Instance.Paused)
            {
                return;
            }

            GetCurrentAim();
            DetermineWeaponRotation();
        }


        /// At fixed update we move the target and reticle
        protected virtual void FixedUpdate()
        {
            if (EnigmaGameManager.Instance.Paused)
            {
                return;
            }

            MoveTarget();
            MoveReticle();
            UpdatePlane();
        }

        protected virtual void UpdatePlane()
        {
            _playerPlane.SetNormalAndPosition(Vector3.up, this.transform.position);
        }


        /// Determines the weapon rotation based on the current aim direction
        protected override void DetermineWeaponRotation()
        {
            if (ReticleMovesWithSlopes)
            {
                if (Vector3.Distance(_slopeTargetPosition, this.transform.position) < MouseDeadZoneRadius)
                {
                    return;
                }

                AimAt(_slopeTargetPosition);

                if (_weaponAimCurrentAim != Vector3.zero)
                {
                    if (_direction != Vector3.zero)
                    {
                        CurrentAngle = Mathf.Atan2(_weaponAimCurrentAim.z, _weaponAimCurrentAim.x) * Mathf.Rad2Deg;
                        CurrentAngleAbsolute = Mathf.Atan2(_weaponAimCurrentAim.y, _weaponAimCurrentAim.x) * Mathf.Rad2Deg;
                        if (RotationMode == RotationModes.Strict4Directions || RotationMode == RotationModes.Strict8Directions)
                        {
                            CurrentAngle = MMMaths.RoundToClosest(CurrentAngle, _possibleAngleValues);
                        }

                        CurrentAngle += _additionalAngle;
                        CurrentAngle = Mathf.Clamp(CurrentAngle, MinimumAngle, MaximumAngle);
                        CurrentAngle = -CurrentAngle + 90f;
                        _lookRotation = Quaternion.Euler(CurrentAngle * Vector3.up);
                    }
                }

                return;
            }

            if (Unrestricted3DAim)
            {
                AimAt(this.transform.position + _weaponAimCurrentAim);
                return;
            }

            if (_weaponAimCurrentAim != Vector3.zero)
            {
                if (_direction != Vector3.zero)
                {
                    CurrentAngle = Mathf.Atan2(_weaponAimCurrentAim.z, _weaponAimCurrentAim.x) * Mathf.Rad2Deg;
                    CurrentAngleAbsolute = Mathf.Atan2(_weaponAimCurrentAim.y, _weaponAimCurrentAim.x) * Mathf.Rad2Deg;
                    if (RotationMode == RotationModes.Strict4Directions || RotationMode == RotationModes.Strict8Directions)
                    {
                        CurrentAngle = MMMaths.RoundToClosest(CurrentAngle, _possibleAngleValues);
                    }

                    // we add our additional angle
                    CurrentAngle += _additionalAngle;

                    // we clamp the angle to the min/max values set in the inspector

                    CurrentAngle = Mathf.Clamp(CurrentAngle, MinimumAngle, MaximumAngle);
                    CurrentAngle = -CurrentAngle + 90f;

                    _lookRotation = Quaternion.Euler(CurrentAngle * Vector3.up);

                    RotateWeapon(_lookRotation);
                }
            }
            else
            {
                CurrentAngle = 0f;
                RotateWeapon(_initialRotation);
            }
        }

        protected override void AimAt(Vector3 target)
        {
            base.AimAt(target);

            _aimAtDirection = target - transform.position;
            _aimAtQuaternion = Quaternion.LookRotation(_aimAtDirection, Vector3.up);
            if (WeaponRotationSpeed == 0f)
            {
                transform.rotation = _aimAtQuaternion;
            }
            else
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, _aimAtQuaternion, WeaponRotationSpeed * Time.deltaTime);
            }
        }


        /// Aims the weapon towards a new point
        /// <param name="newAim">New aim.</param>
        public override void SetCurrentAim(Vector3 newAim, bool setAimAsLastNonNullMovement = false)
        {
            if (!AimControlActive)
            {
                return;
            }

            base.SetCurrentAim(newAim, setAimAsLastNonNullMovement);

            _lastNonNullMovement.x = newAim.x;
            _lastNonNullMovement.y = newAim.z;
        }


        /// Initializes the reticle based on the settings defined in the inspector
        protected override void InitializeReticle()
        {
            if (EnigmaWeapon.Owner == null)
            {
                return;
            }

            if (Reticle == null)
            {
                return;
            }

            if (ReticleType == ReticleTypes.None)
            {
                return;
            }

            if (_reticle != null)
            {
                Destroy(_reticle);
            }

            if (ReticleType == ReticleTypes.Scene)
            {
                _reticle = (GameObject)Instantiate(Reticle);

                if (!ReticleAtMousePosition)
                {
                    if (EnigmaWeapon.Owner != null)
                    {
                        _reticle.transform.SetParent(EnigmaWeapon.transform);
                        _reticle.transform.localPosition = ReticleDistance * Vector3.forward;
                    }
                }
            }

            if (ReticleType == ReticleTypes.UI)
            {
                _reticle = (GameObject)Instantiate(Reticle);
                _reticle.transform.SetParent(EnigmaGUIManager.Instance.MainCanvas.transform);
                _reticle.transform.localScale = Vector3.one;
                if (_reticle.gameObject.MMGetComponentNoAlloc<MMUIFollowMouse>() != null)
                {
                    _reticle.gameObject.MMGetComponentNoAlloc<MMUIFollowMouse>().TargetCanvas = EnigmaGUIManager.Instance.MainCanvas;
                }
            }
        }


        /// Every frame, moves the reticle if it's been told to follow the pointer
        protected override void MoveReticle()
        {
            if (ReticleType == ReticleTypes.None)
            {
                return;
            }

            if (_reticle == null)
            {
                return;
            }

            if (EnigmaWeapon.Owner.ConditionState.CurrentState == EnigmaCharacterStates.CharacterConditions.Paused)
            {
                return;
            }

            if (ReticleType == ReticleTypes.Scene)
            {
                // if we're not supposed to rotate the reticle, we force its rotation, otherwise we apply the current look rotation
                if (!RotateReticle)
                {
                    _reticle.transform.rotation = Quaternion.identity;
                }
                else
                {
                    if (ReticleAtMousePosition)
                    {
                        _reticle.transform.rotation = _lookRotation;
                    }
                }

                // if we're in follow mouse mode and the current control scheme is mouse, we move the reticle to the mouse's position
                if (ReticleAtMousePosition && AimControl == AimControls.Mouse)
                {
                    _reticle.transform.position = MMMaths.Lerp(_reticle.transform.position, _reticlePosition, 0.3f, Time.deltaTime);
                }
            }

            _reticlePosition = _reticle.transform.position;

            if (ReticleMovesWithSlopes)
            {
                // we cast a ray from above
                RaycastHit groundCheck = MMDebug.Raycast3D(_reticlePosition + Vector3.up * MaximumSlopeElevation / 2f, Vector3.down, MaximumSlopeElevation, ReticleObstacleMask, Color.cyan, true);
                if (groundCheck.collider != null)
                {
                    _reticlePosition.y = groundCheck.point.y + ReticleHeight;
                    _reticle.transform.position = _reticlePosition;

                    _slopeTargetPosition = groundCheck.point + Vector3.up * ReticleHeight;
                }
                else
                {
                    _slopeTargetPosition = _reticle.transform.position;
                }
            }
        }

        protected override void MoveTarget()
        {
            if (EnigmaWeapon.Owner == null)
            {
                return;
            }

            if (MoveCameraTargetTowardsReticle)
            {
                if (ReticleType != ReticleTypes.None)
                {
                    _newCamTargetPosition = _reticlePosition;
                    _newCamTargetDirection = _newCamTargetPosition - this.transform.position;
                    if (_newCamTargetDirection.sqrMagnitude > (CameraTargetMaxDistance * CameraTargetMaxDistance))
                    {
                        _newCamTargetDirection = _newCamTargetDirection.normalized * CameraTargetMaxDistance;
                    }

                    _newCamTargetPosition = this.transform.position + _newCamTargetDirection;

                    _newCamTargetPosition = Vector3.Lerp(EnigmaWeapon.Owner.CameraTarget.transform.position, Vector3.Lerp(this.transform.position, _newCamTargetPosition, CameraTargetOffset), Time.deltaTime * CameraTargetSpeed);

                    EnigmaWeapon.Owner.CameraTarget.transform.position = _newCamTargetPosition;
                }
                else
                {
                    _newCamTargetPosition = this.transform.position + CurrentAim.normalized * CameraTargetMaxDistance;
                    _newCamTargetDirection = _newCamTargetPosition - this.transform.position;

                    _newCamTargetPosition = this.transform.position + _newCamTargetDirection;

                    _newCamTargetPosition = Vector3.Lerp(EnigmaWeapon.Owner.CameraTarget.transform.position, Vector3.Lerp(this.transform.position, _newCamTargetPosition, CameraTargetOffset), Time.deltaTime * CameraTargetSpeed);

                    EnigmaWeapon.Owner.CameraTarget.transform.position = _newCamTargetPosition;
                }
            }
        }
    }
}