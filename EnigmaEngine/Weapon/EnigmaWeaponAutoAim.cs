using System;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    /// An abstract class, meant to be extended for 2D and 3D specifics, handling the basics of auto aim. 
    /// Extended components should be placed on a weapon with an aim component
    [RequireComponent(typeof(EnigmaWeapon))]
    public abstract class EnigmaWeaponAutoAim : MonoBehaviour
    {
        [Title("Layer Masks")]
        /// the layermask on which to look for aim targets
        [Tooltip("The layermask on which to look for aim targets")]
        public LayerMask TargetsMask;

        /// the layermask on which to look for obstacles
        [Tooltip("The layermask on which to look for obstacles")]
        public LayerMask ObstacleMask = EnigmaLayerManager.ObstaclesLayerMask;

        [Title("Scan for Targets")] 
        [Tooltip("The radius (in units) around the character within which to search for targets")]
        public float ScanRadius = 15f;

        [Tooltip("The size of the boxcast that will be performed to verify line of fire")]
        public Vector2 LineOfFireBoxcastSize = new Vector2(0.1f, 0.1f);

        [Tooltip("The duration (in seconds) between 2 scans for targets")]
        public float DurationBetweenScans = 1f;

        [Tooltip("An offset to apply to the weapon's position for scan ")]
        public Vector3 DetectionOriginOffset = Vector3.zero;

        [Tooltip("If this is true, auto aim scan will only acquire new targets if the owner is in the idle state")]
        public bool OnlyAcquireTargetsIfOwnerIsIdle = false;

        [Title("Weapon Rotation")] 
        [Tooltip("The rotation mode to apply when a target is found")]
        public EnigmaWeaponAim.RotationModes RotationMode;

        [Tooltip("If this is true, the auto aim direction will also be passed as the last non null direction, so the weapon will keep aiming in that direction should the target be lost")]
        public bool ApplyAutoAimAsLastDirection = true;

        [Title("Camera Target")] [Tooltip("Whether or not this component should take control of the camera target when a camera is found")]
        public bool MoveCameraTarget = true;

        [Tooltip("The normalized distance (between 0 and 1) at which the camera target should be, on a line going from the weapon owner (0) to the auto aim target (1)")]
        [Range(0f, 1f)]
        public float CameraTargetDistance = 0.5f;

        [Tooltip("The maximum distance from the weapon owner at which the camera target can be")] [MMCondition("MoveCameraTarget", true)]
        public float CameraTargetMaxDistance = 10f;

        [Tooltip("the speed at which to move the camera target")] [MMCondition("MoveCameraTarget", true)]
        public float CameraTargetSpeed = 5f;

        [Tooltip("If this is true, the camera target will move back to the character if no target is found")] [MMCondition("MoveCameraTarget", true)]
        public bool MoveCameraToCharacterIfNoTarget = false;

        [Title("Aim Marker")] 
        [Tooltip("An AimMarker prefab to use to show where this auto aim weapon is aiming")]
        public EnigmaAimMarker AimMarkerPrefab;

        [Tooltip("If this is true, the aim marker will be removed when the weapon gets destroyed")]
        public bool DestroyAimMarkerOnWeaponDestroy = true;

        [Title("Feedback")] 
        [Tooltip("A feedback to play when a target is found and we didn't have one already")]
        public MMFeedbacks FirstTargetFoundFeedback;

        [Tooltip("A feedback to play when we already had a target and just found a new one")]
        public MMFeedbacks NewTargetFoundFeedback;

        [Tooltip("A feedback to play when no more targets are found, and we just lost our last target")]
        public MMFeedbacks NoMoreTargetsFeedback;

        [Title("Debug")]
        [Tooltip("the current target of the auto aim module")] 
        [ReadOnly]
        public Transform Target;

        [Tooltip("Whether or not to draw a debug sphere around the weapon to show its aim radius")]
        public bool DrawDebugRadius = true;

        protected float _lastScanTimestamp = 0f;
        protected EnigmaWeaponAim _weaponAim;
        protected EnigmaWeaponAim.AimControls _originalAimControl;
        protected EnigmaWeaponAim.RotationModes _originalRotationMode;
        protected Vector3 _raycastOrigin;
        protected EnigmaWeapon _weapon;
        protected bool _originalMoveCameraTarget;
        protected Transform _targetLastFrame;
        protected EnigmaAimMarker _aimMarker;


        /// On Awake we initialize our component
        protected virtual void Start()
        {
            Initialization();
        }


        /// On init we grab our WeaponAim
        protected virtual void Initialization()
        {
            _weaponAim = this.gameObject.GetComponent<EnigmaWeaponAim>();
            _weapon = this.gameObject.GetComponent<EnigmaWeapon>();
            _isOwnerNull = _weapon.Owner == null;
            if (_weaponAim == null)
            {
                Debug.LogWarning(this.name +
                                 " : the WeaponAutoAim on this object requires that you add either a WeaponAim2D or WeaponAim3D component to your weapon.");
                return;
            }

            _originalAimControl = _weaponAim.AimControl;
            _originalRotationMode = _weaponAim.RotationMode;
            _originalMoveCameraTarget = _weaponAim.MoveCameraTargetTowardsReticle;

            FirstTargetFoundFeedback?.Initialization(this.gameObject);
            NewTargetFoundFeedback?.Initialization(this.gameObject);
            NoMoreTargetsFeedback?.Initialization(this.gameObject);

            if (AimMarkerPrefab != null)
            {
                _aimMarker = Instantiate(AimMarkerPrefab);
                _aimMarker.name = this.gameObject.name + "_AimMarker";
                _aimMarker.Disable();
            }
        }


        /// On Update, we setup our ray origin, scan periodically and set aim if needed
        protected virtual void Update()
        {
            if (_weaponAim == null)
            {
                return;
            }

            DetermineRaycastOrigin();
            ScanIfNeeded();
            HandleTarget();
            HandleMoveCameraTarget();
            HandleTargetChange();
            _targetLastFrame = Target;
        }


        /// A method used to compute the origin of the detection casts
        protected abstract void DetermineRaycastOrigin();


        /// This method should define how the scan for targets is performed
        protected abstract bool ScanForTargets();


        public virtual bool CanAcquireNewTargets()
        {
            if (OnlyAcquireTargetsIfOwnerIsIdle && !_isOwnerNull)
            {
                if (_weapon.Owner.MovementState.CurrentState != EnigmaCharacterStates.MovementStates.Idle)
                {
                    return false;
                }
            }

            return true;
        }


        /// Sends aim coordinates to the weapon aim component
        protected abstract void SetAim();


        /// Moves the camera target towards the auto aim target if needed
        protected Vector3 _newCamTargetPosition;

        protected Vector3 _newCamTargetDirection;
        protected bool _isOwnerNull;


        /// Checks for target changes and triggers the appropriate methods if needed
        protected virtual void HandleTargetChange()
        {
            if (Target == _targetLastFrame)
            {
                return;
            }

            if (_aimMarker != null)
            {
                _aimMarker.SetTarget(Target);
            }

            if (Target == null)
            {
                NoMoreTargets();
                return;
            }

            if (_targetLastFrame == null)
            {
                FirstTargetFound();
                return;
            }

            if ((_targetLastFrame != null) && (Target != null))
            {
                NewTargetFound();
            }
        }


        /// When no more targets are found, and we just lost one, we play a dedicated feedback
        protected virtual void NoMoreTargets()
        {
            NoMoreTargetsFeedback?.PlayFeedbacks();
        }


        /// When a new target is found and we didn't have one already, we play a dedicated feedback
        protected virtual void FirstTargetFound()
        {
            FirstTargetFoundFeedback?.PlayFeedbacks();
        }


        /// When a new target is found, and we previously had another, we play a dedicated feedback
        protected virtual void NewTargetFound()
        {
            NewTargetFoundFeedback?.PlayFeedbacks();
        }


        /// Moves the camera target if needed
        protected virtual void HandleMoveCameraTarget()
        {
            bool targetIsNull = (Target == null);

            if (!MoveCameraTarget || (_isOwnerNull))
            {
                return;
            }

            if (!MoveCameraToCharacterIfNoTarget && targetIsNull)
            {
                return;
            }

            if (targetIsNull)
            {
                _newCamTargetPosition = _weapon.Owner.transform.position;
            }
            else
            {
                _newCamTargetPosition = Vector3.Lerp(_weapon.Owner.transform.position, Target.transform.position,
                    CameraTargetDistance);
            }

            _newCamTargetDirection = _newCamTargetPosition - this.transform.position;

            if (_newCamTargetDirection.magnitude > CameraTargetMaxDistance)
            {
                _newCamTargetDirection = _newCamTargetDirection.normalized * CameraTargetMaxDistance;
            }

            _newCamTargetPosition = this.transform.position + _newCamTargetDirection;

            _newCamTargetPosition = Vector3.Lerp(_weapon.Owner.CameraTarget.transform.position,
                _newCamTargetPosition,
                Time.deltaTime * CameraTargetSpeed);

            _weapon.Owner.CameraTarget.transform.position = _newCamTargetPosition;
        }


        /// Performs a periodic scan
        protected virtual void ScanIfNeeded()
        {
            if (Time.time - _lastScanTimestamp > DurationBetweenScans)
            {
                ScanForTargets();
                _lastScanTimestamp = Time.time;
            }
        }


        /// Sets aim if needed, otherwise reverts to the previous aim control mode
        protected virtual void HandleTarget()
        {
            if (Target == null)
            {
                _weaponAim.AimControl = _originalAimControl;
                _weaponAim.RotationMode = _originalRotationMode;
                _weaponAim.MoveCameraTargetTowardsReticle = _originalMoveCameraTarget;
            }
            else
            {
                _weaponAim.AimControl = EnigmaWeaponAim.AimControls.Script;
                _weaponAim.RotationMode = RotationMode;
                if (MoveCameraTarget)
                {
                    _weaponAim.MoveCameraTargetTowardsReticle = false;
                }

                SetAim();
            }
        }


        /// Draws a sphere around the weapon to show its auto aim radius
        protected virtual void OnDrawGizmos()
        {
            if (DrawDebugRadius)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_raycastOrigin, ScanRadius);
            }
        }


        /// On Disable, we hide our aim marker if needed
        protected virtual void OnDisable()
        {
            if (_aimMarker != null)
            {
                _aimMarker.Disable();
            }
        }

        protected void OnDestroy()
        {
            if (DestroyAimMarkerOnWeaponDestroy && (_aimMarker != null))
            {
                Destroy(_aimMarker.gameObject);
            }
        }
    }
}