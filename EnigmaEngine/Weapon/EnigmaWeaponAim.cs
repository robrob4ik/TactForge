
using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [RequireComponent(typeof(EnigmaWeapon))]
    public abstract class EnigmaWeaponAim : MonoBehaviour, MMEventListener<EnigmaEngineEvent>
    {
        public enum AimControls
        {
            Off,
            PrimaryMovement,
            SecondaryMovement,
            Mouse,
            Script,
        }

        public enum RotationModes
        {
            Free,
            Strict2Directions,
            Strict4Directions,
            Strict8Directions
        }

        public enum ReticleTypes
        {
            None,
            Scene,
            UI
        }

        
        [FoldoutGroup("Control Mode"), Title("Aim Controls")]
        [Tooltip("The selected aim control mode")]
        public AimControls AimControl = AimControls.SecondaryMovement;

        [FoldoutGroup("Control Mode")]
        [Tooltip("If this is true, this script will be able to read input from its specified AimControl mode")]
        public bool AimControlActive = true;

        [FoldoutGroup("Weapon Rotation"), Title("Weapon Rotation")]
        [Tooltip("The rotation mode")]
        public RotationModes RotationMode = RotationModes.Free;

        [FoldoutGroup("Weapon Rotation")]
        [Tooltip("The the speed at which the weapon reaches its new position. Set it to zero if you want movement to directly follow input")]
        public float WeaponRotationSpeed = 1f;

        [FoldoutGroup("Weapon Rotation")]
        [Range(-180, 180)]
        [Tooltip("The minimum angle at which the weapon's rotation will be clamped")]
        public float MinimumAngle = -180f;

        [FoldoutGroup("Weapon Rotation")]
        [Range(-180, 180)]
        [Tooltip("The maximum angle at which the weapon's rotation will be clamped")]
        public float MaximumAngle = 180f;

        [FoldoutGroup("Weapon Rotation")]
        [Tooltip("The minimum threshold at which the weapon's rotation magnitude will be considered ")]
        public float MinimumMagnitude = 0.2f;

        [FoldoutGroup("Reticle"), Title("Reticle")]
        [Tooltip("Defines whether the reticle is placed in the scene or in the UI")]
        public ReticleTypes ReticleType = ReticleTypes.None;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("The gameobject to display as the aim's reticle/crosshair. Leave it blank if you don't want a reticle")]
        public GameObject Reticle;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene")]
        [Tooltip("The distance at which the reticle will be from the weapon")]
        public float ReticleDistance;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("The height at which the reticle should position itself above the ground, when in Scene mode")]
        public float ReticleHeight;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene")]
        [Tooltip("If set to true, the reticle will be placed at the mouse's position (like a pointer)")]
        public bool ReticleAtMousePosition;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene")]
        [Tooltip("If set to true, the reticle will rotate on itself to reflect the weapon's rotation. If not it'll remain stable.")]
        public bool RotateReticle = false;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("If set to true, the reticle will replace the mouse pointer")]
        public bool ReplaceMousePointer = true;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("The radius around the weapon rotation centre where the mouse will be ignored, to avoid glitches")]
        public float MouseDeadZoneRadius = 0.5f;

        [FoldoutGroup("Reticle")]
        [ShowIf("@ReticleType == ReticleTypes.Scene || ReticleType == ReticleTypes.UI")]
        [Tooltip("If set to false, the reticle won't be added and displayed")]
        public bool DisplayReticle = true;

        [FoldoutGroup("Camera Target"), Title("Camera Target")]
        [Tooltip("Whether the camera target should be moved towards the reticle to provide a better vision of the possible target. If you don't have a reticle, it'll be moved towards your aim direction.")]
        public bool MoveCameraTargetTowardsReticle = false;

        [FoldoutGroup("Camera Target")]
        [Range(0f, 1f)]
        [Tooltip("The offset to apply to the camera target along the transform / reticle line")]
        public float CameraTargetOffset = 0.3f;

        [FoldoutGroup("Camera Target")]
        [ShowIf("MoveCameraTargetTowardsReticle")]
        [Tooltip("The maximum distance at which to move the camera target from the weapon")]
        public float CameraTargetMaxDistance = 10f;

        [FoldoutGroup("Camera Target")]
        [ShowIf("MoveCameraTargetTowardsReticle")]
        [Tooltip("The speed at which the camera target should be moved")]
        public float CameraTargetSpeed = 5f;

        public virtual float CurrentAngleAbsolute { get; protected set; }

        public virtual Quaternion CurrentRotation
        {
            get { return transform.rotation; }
        }

        public virtual Vector3 CurrentAim
        {
            get { return _currentAim; }
        }

        /// the weapon's current direction, absolute (flip independent)
        public virtual Vector3 CurrentAimAbsolute
        {
            get { return _currentAimAbsolute; }
        }

        /// the current angle the weapon is aiming at
        public virtual float CurrentAngle { get; protected set; }

        /// the current angle the weapon is aiming at, adjusted to compensate for the current orientation of the character
        public virtual float CurrentAngleRelative
        {
            get
            {
                if (EnigmaWeapon != null)
                {
                    if (EnigmaWeapon.Owner != null)
                    {
                        return CurrentAngle;
                    }
                }

                return 0;
            }
        }

        public virtual EnigmaWeapon targetEnigmaWeapon => EnigmaWeapon;

        protected Camera _mainCamera;
        protected Vector2 _lastNonNullMovement;
        protected EnigmaWeapon EnigmaWeapon;
        protected Vector3 _currentAim = Vector3.zero;
        protected Vector3 _currentAimAbsolute = Vector3.zero;
        protected Quaternion _lookRotation;
        protected Vector3 _direction;
        protected float[] _possibleAngleValues;
        protected Vector3 _mousePosition;
        protected Vector3 _lastMousePosition;
        protected float _additionalAngle;
        protected Quaternion _initialRotation;
        protected Plane _playerPlane;
        protected GameObject _reticle;
        protected Vector3 _reticlePosition;
        protected Vector3 _newCamTargetPosition;
        protected Vector3 _newCamTargetDirection;
        protected bool _initialized = false;
        
        protected virtual void Start()
        {
            Initialization();
        }
        
        protected virtual void Initialization()
        {
            EnigmaWeapon = GetComponent<EnigmaWeapon>();
            _mainCamera = Camera.main;

            if (RotationMode == RotationModes.Strict4Directions)
            {
                _possibleAngleValues = new float[5];
                _possibleAngleValues[0] = -180f;
                _possibleAngleValues[1] = -90f;
                _possibleAngleValues[2] = 0f;
                _possibleAngleValues[3] = 90f;
                _possibleAngleValues[4] = 180f;
            }

            if (RotationMode == RotationModes.Strict8Directions)
            {
                _possibleAngleValues = new float[9];
                _possibleAngleValues[0] = -180f;
                _possibleAngleValues[1] = -135f;
                _possibleAngleValues[2] = -90f;
                _possibleAngleValues[3] = -45f;
                _possibleAngleValues[4] = 0f;
                _possibleAngleValues[5] = 45f;
                _possibleAngleValues[6] = 90f;
                _possibleAngleValues[7] = 135f;
                _possibleAngleValues[8] = 180f;
            }

            _initialRotation = transform.rotation;
            InitializeReticle();
            _playerPlane = new Plane(Vector3.up, Vector3.zero);
            _initialized = true;
        }

        public virtual void ApplyAim()
        {
            Initialization();
            GetCurrentAim();
            DetermineWeaponRotation();
        }


        /// Aims the weapon towards a new point
        /// <param name="newAim">New aim.</param>
        public virtual void SetCurrentAim(Vector3 newAim, bool setAimAsLastNonNullMovement = false)
        {
            _currentAim = newAim;
        }

        protected virtual void GetCurrentAim()
        {
        }


        /// Every frame, we compute the aim direction and rotate the weapon accordingly
        protected virtual void Update()
        {
        }


        /// On LateUpdate, resets any additional angle
        protected virtual void LateUpdate()
        {
            ResetAdditionalAngle();
        }


        /// Determines the weapon's rotation
        protected virtual void DetermineWeaponRotation()
        {
        }


        /// Moves the weapon's reticle
        protected virtual void MoveReticle()
        {
        }


        /// Returns the position of the reticle
        /// <returns></returns>
        public virtual Vector3 GetReticlePosition()
        {
            return _reticle.transform.position;
        }


        /// Returns the current mouse position
        public virtual Vector3 GetMousePosition()
        {
            return _mainCamera.ScreenToWorldPoint(_mousePosition);
        }


        /// Rotates the weapon, optionnally applying a lerp to it.
        /// <param name="newRotation">New rotation.</param>
        protected virtual void RotateWeapon(Quaternion newRotation, bool forceInstant = false)
        {
            if (EnigmaGameManager.Instance.Paused)
            {
                return;
            }

            // if the rotation speed is == 0, we have instant rotation
            if ((WeaponRotationSpeed == 0f) || forceInstant)
            {
                transform.rotation = newRotation;
            }
            // otherwise we lerp the rotation
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, WeaponRotationSpeed * Time.deltaTime);
            }
        }

        protected Vector3 _aimAtDirection;
        protected Quaternion _aimAtQuaternion;

        protected virtual void AimAt(Vector3 target)
        {
        }


        /// If a reticle has been set, instantiates the reticle and positions it
        protected virtual void InitializeReticle()
        {
        }


        /// This method defines how the character's camera target should move
        protected virtual void MoveTarget()
        {
        }


        /// Removes any remaining reticle
        public virtual void RemoveReticle()
        {
            if (_reticle != null)
            {
                Destroy(_reticle.gameObject);
            }
        }


        /// Hides (or shows) the reticle based on the DisplayReticle setting
        protected virtual void HideReticle()
        {
            if (_reticle != null)
            {
                if (EnigmaGameManager.Instance.Paused)
                {
                    _reticle.gameObject.SetActive(false);
                    return;
                }

                _reticle.gameObject.SetActive(DisplayReticle);
            }
        }


        /// Hides or show the mouse pointer based on the settings
        protected virtual void HideMousePointer()
        {
            if (AimControl != AimControls.Mouse)
            {
                return;
            }

            if (EnigmaGameManager.Instance.Paused)
            {
                Cursor.visible = true;
                return;
            }

            if (ReplaceMousePointer)
            {
                Cursor.visible = false;
            }
            else
            {
                Cursor.visible = true;
            }
        }


        /// On Destroy, we reinstate our cursor if needed
        protected void OnDestroy()
        {
            if (ReplaceMousePointer)
            {
                Cursor.visible = true;
            }
        }


        /// Adds additional angle to the weapon's rotation
        /// <param name="addedAngle"></param>
        public virtual void AddAdditionalAngle(float addedAngle)
        {
            _additionalAngle += addedAngle;
        }


        /// Resets the additional angle
        protected virtual void ResetAdditionalAngle()
        {
            _additionalAngle = 0;
        }

        protected virtual void AutoDetectWeaponMode()
        {
            if (EnigmaWeapon.Owner.LinkedInputManager != null)
            {
                if ((EnigmaWeapon.Owner.LinkedInputManager.ForceWeaponMode) && (AimControl != AimControls.Off))
                {
                    AimControl = EnigmaWeapon.Owner.LinkedInputManager.WeaponForcedMode;
                }

                if ((!EnigmaWeapon.Owner.LinkedInputManager.ForceWeaponMode) && (EnigmaWeapon.Owner.LinkedInputManager.IsMobile) && (AimControl == AimControls.Mouse))
                {
                    AimControl = AimControls.PrimaryMovement;
                }
            }
        }

        public void OnMMEvent(EnigmaEngineEvent engineEvent)
        {
            switch (engineEvent.EventType)
            {
                case EnigmaEngineEventTypes.LevelStart:
                    _initialized = false;
                    Initialization();
                    break;
            }
        }
        
        protected virtual void OnEnable()
        {
            this.MMEventStartListening<EnigmaEngineEvent>();
        }
        
        protected virtual void OnDisable()
        {
            this.MMEventStopListening<EnigmaEngineEvent>();
        }
    }
}