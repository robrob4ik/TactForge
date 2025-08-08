using UnityEngine;
using MoreMountains.Tools;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    /// IMPORTANT : this script's Execution Order MUST be -100.
    [AddComponentMenu("Enigma Engine/Managers/Enigma Input Manager")]
    public class EnigmaInputManager : MMSingleton<EnigmaInputManager>
    {
        [Title("Settings")]
        public bool InputDetectionActive = true;

        [Tooltip("If this is true, button states will be reset on focus loss - when clicking outside the player window on PC, for example")]
        public bool ResetButtonStatesOnFocusLoss = true;

        [Title("Player binding")]
        [Tooltip("A string identifying the target player(s). You'll need to set this exact same string on your Character, and set its type to Player")]
        public string PlayerID = "Player1";

        public enum InputForcedModes
        {
            None,
            Mobile,
            Desktop
        }

        public enum MovementControls
        {
            Joystick,
            Arrows
        }

        [Title("Mobile controls")]
        [Tooltip("Use this to force desktop (keyboard, pad) or mobile (touch) mode")]
        public InputForcedModes InputForcedMode;

        [Tooltip("If this is true, the weapon mode will be forced to the selected WeaponForcedMode")]
        public bool ForceWeaponMode = false;

        [MMCondition("ForceWeaponMode", true)]
        [Tooltip("use this to force a control mode for weapons")]
        public EnigmaWeaponAim.AimControls WeaponForcedMode;

        [Tooltip("If this is true, mobile controls will be hidden in editor mode, regardless of the current build target or the forced mode")]
        public bool HideMobileControlsInEditor = false;

        [Tooltip("use this to specify whether you want to use the default joystick or arrows to move your character")]
        public MovementControls MovementControl = MovementControls.Joystick;

        [Tooltip("If this is true, the mobile controls will be hidden when the primary desktop axis is active, and the input manager will switch to desktop inputs")]
        public bool ForceDesktopIfPrimaryAxisActive = false;

        [Tooltip("If this is true, the system will revert to mobile controls if the primary axis is inactive for more than AutoRevertToMobileIfPrimaryAxisInactiveDuration")]
        [MMCondition("ForceDesktopIfPrimaryAxisActive", true)]
        public bool AutoRevertToMobileIfPrimaryAxisInactive;

        [Tooltip("the duration, in seconds, after which the system will revert to mobile controls if the primary axis is inactive")]
        [MMCondition("AutoRevertToMobileIfPrimaryAxisInactive", true)]
        public float AutoRevertToMobileIfPrimaryAxisInactiveDuration = 10f;

        public virtual bool IsMobile { get; protected set; }

        public virtual bool IsPrimaryAxisActive { get; protected set; }

        [Title("Movement settings")]
        [MMInformation("Turn SmoothMovement on to have inertia in your controls (meaning there'll be a small delay between a press/release of a direction and your character moving/stopping). You can also define here the horizontal and vertical thresholds.",
            MMInformationAttribute.InformationType.Info, false)]
        [Tooltip("If set to true, acceleration / deceleration will take place when moving / stopping")]
        public bool SmoothMovement = true;

        /// the minimum horizontal and vertical value you need to reach to trigger movement on an analog controller (joystick for example)
        [Tooltip("the minimum horizontal and vertical value you need to reach to trigger movement on an analog controller (joystick for example)")]
        public Vector2 Threshold = new Vector2(0.1f, 0.4f);

        [Title("Camera Rotation")]
        [MMInformation("Here you can decide whether or not camera rotation should impact your input. That can be useful in, for example, a 3D isometric game, if you want 'up' to mean some other direction than Vector3.up/forward.",
            MMInformationAttribute.InformationType.Info, false)]
        /// if this is true, any directional input coming into this input manager will be rotated to align with the current camera orientation
        [Tooltip("If this is true, any directional input coming into this input manager will be rotated to align with the current camera orientation")]
        public bool RotateInputBasedOnCameraDirection = false;

        /// the jump button, used for jumps and validation
        public virtual MMInput.IMButton JumpButton { get; protected set; }

        /// the run button
        public virtual MMInput.IMButton RunButton { get; protected set; }

        /// the dash button
        public virtual MMInput.IMButton DashButton { get; protected set; }

        /// the crouch button
        public virtual MMInput.IMButton CrouchButton { get; protected set; }

        /// the shoot button
        public virtual MMInput.IMButton ShootButton { get; protected set; }

        /// the activate button, used for interactions with zones
        public virtual MMInput.IMButton InteractButton { get; protected set; }

        /// the shoot button
        public virtual MMInput.IMButton SecondaryShootButton { get; protected set; }

        /// the reload button
        public virtual MMInput.IMButton ReloadButton { get; protected set; }

        /// the pause button
        public virtual MMInput.IMButton PauseButton { get; protected set; }

        /// the time control button
        public virtual MMInput.IMButton TimeControlButton { get; protected set; }

        /// the button used to switch character (either via model or prefab switch)
        public virtual MMInput.IMButton SwitchCharacterButton { get; protected set; }

        /// the switch weapon button
        public virtual MMInput.IMButton SwitchWeaponButton { get; protected set; }

        /// the shoot axis, used as a button (non analogic)
        public virtual MMInput.ButtonStates ShootAxis { get; protected set; }

        /// the shoot axis, used as a button (non analogic)
        public virtual MMInput.ButtonStates SecondaryShootAxis { get; protected set; }

        /// the primary movement value (used to move the character around)
        public virtual Vector2 PrimaryMovement { get { return _primaryMovement; } }

        /// the secondary movement (usually the right stick on a gamepad), used to aim
        public virtual Vector2 SecondaryMovement { get { return _secondaryMovement; } }

        /// the primary movement value (used to move the character around)
        public virtual Vector2 LastNonNullPrimaryMovement { get; set; }

        /// the secondary movement (usually the right stick on a gamepad), used to aim
        public virtual Vector2 LastNonNullSecondaryMovement { get; set; }

        /// the camera rotation axis input value
        public virtual float CameraRotationInput { get { return _cameraRotationInput; } }

        /// the current camera angle
        public virtual float CameraAngle { get { return _cameraAngle; } }

        /// the position of the mouse
        public virtual Vector2 MousePosition => Input.mousePosition;

        protected Camera _targetCamera;
        protected bool _camera3D;
        protected float _cameraAngle;
        protected List<MMInput.IMButton> ButtonList;
        protected Vector2 _primaryMovement = Vector2.zero;
        protected Vector2 _secondaryMovement = Vector2.zero;
        protected float _cameraRotationInput = 0f;
        protected string _axisHorizontal;
        protected string _axisVertical;
        protected string _axisSecondaryHorizontal;
        protected string _axisSecondaryVertical;
        protected string _axisShoot;
        protected string _axisShootSecondary;
        protected string _axisCamera;
        protected float _primaryAxisActiveTimestamp;


        /// Statics initialization to support enter play modes
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        protected static void InitializeStatics() { _instance = null; }


        /// On Awake we run our pre-initialization
        protected override void Awake()
        {
            base.Awake();
            PreInitialization();
        }


        /// On Start we look for what mode to use, and initialize our axis and buttons
        protected virtual void Start() { Initialization(); }


        /// Initializes buttons and axis
        protected virtual void PreInitialization()
        {
            InitializeButtons();
            InitializeAxis();
        }


        /// On init we auto detect control schemes
        protected virtual void Initialization() {  }

        /// Initializes the buttons. If you want to add more buttons, make sure to register them here.
        protected virtual void InitializeButtons()
        {
            ButtonList = new List<MMInput.IMButton>();
            ButtonList.Add(JumpButton = new MMInput.IMButton(PlayerID, "Jump", JumpButtonDown, JumpButtonPressed, JumpButtonUp));
            ButtonList.Add(RunButton = new MMInput.IMButton(PlayerID, "Run", RunButtonDown, RunButtonPressed, RunButtonUp));
            ButtonList.Add(InteractButton = new MMInput.IMButton(PlayerID, "Interact", InteractButtonDown, InteractButtonPressed, InteractButtonUp));
            ButtonList.Add(DashButton = new MMInput.IMButton(PlayerID, "Dash", DashButtonDown, DashButtonPressed, DashButtonUp));
            ButtonList.Add(CrouchButton = new MMInput.IMButton(PlayerID, "Crouch", CrouchButtonDown, CrouchButtonPressed, CrouchButtonUp));
            ButtonList.Add(SecondaryShootButton = new MMInput.IMButton(PlayerID, "SecondaryShoot", SecondaryShootButtonDown, SecondaryShootButtonPressed, SecondaryShootButtonUp));
            ButtonList.Add(ShootButton = new MMInput.IMButton(PlayerID, "Shoot", ShootButtonDown, ShootButtonPressed, ShootButtonUp));
            ButtonList.Add(ReloadButton = new MMInput.IMButton(PlayerID, "Reload", ReloadButtonDown, ReloadButtonPressed, ReloadButtonUp));
            ButtonList.Add(SwitchWeaponButton = new MMInput.IMButton(PlayerID, "SwitchWeapon", SwitchWeaponButtonDown, SwitchWeaponButtonPressed, SwitchWeaponButtonUp));
            ButtonList.Add(PauseButton = new MMInput.IMButton(PlayerID, "Pause", PauseButtonDown, PauseButtonPressed, PauseButtonUp));
            ButtonList.Add(TimeControlButton = new MMInput.IMButton(PlayerID, "TimeControl", TimeControlButtonDown, TimeControlButtonPressed, TimeControlButtonUp));
            ButtonList.Add(SwitchCharacterButton = new MMInput.IMButton(PlayerID, "SwitchCharacter", SwitchCharacterButtonDown, SwitchCharacterButtonPressed, SwitchCharacterButtonUp));
        }


        /// Initializes the axis strings.
        protected virtual void InitializeAxis()
        {
            _axisHorizontal = PlayerID + "_Horizontal";
            _axisVertical = PlayerID + "_Vertical";
            _axisSecondaryHorizontal = PlayerID + "_SecondaryHorizontal";
            _axisSecondaryVertical = PlayerID + "_SecondaryVertical";
            _axisShoot = PlayerID + "_ShootAxis";
            _axisShootSecondary = PlayerID + "_SecondaryShootAxis";
            _axisCamera = PlayerID + "_CameraRotationAxis";
        }


        /// On LateUpdate, we process our button states
        protected virtual void LateUpdate() { ProcessButtonStates(); }


        /// At update, we check the various commands and update our values and states accordingly.
        protected virtual void Update()
        {
            if (!IsMobile && InputDetectionActive)
            {
                SetMovement();
                SetSecondaryMovement();
                SetShootAxis();
                SetCameraRotationAxis();
                GetInputButtons();
            }

            GetLastNonNullValues();
        }

   
        /// Gets the last non null values for both primary and secondary axis
        protected virtual void GetLastNonNullValues()
        {
            if (_primaryMovement.magnitude > Threshold.x) { LastNonNullPrimaryMovement = _primaryMovement; }

            if (_secondaryMovement.magnitude > Threshold.x) { LastNonNullSecondaryMovement = _secondaryMovement; }
        }


        /// If we're not on mobile, watches for input changes, and updates our buttons states accordingly
        protected virtual void GetInputButtons()
        {
            foreach (MMInput.IMButton button in ButtonList)
            {
                if (Input.GetButton(button.ButtonID)) { button.TriggerButtonPressed(); }

                if (Input.GetButtonDown(button.ButtonID)) { button.TriggerButtonDown(); }

                if (Input.GetButtonUp(button.ButtonID)) { button.TriggerButtonUp(); }
            }
        }


        /// Called at LateUpdate(), this method processes the button states of all registered buttons
        public virtual void ProcessButtonStates()
        {
            // for each button, if we were at ButtonDown this frame, we go to ButtonPressed. If we were at ButtonUp, we're now Off
            foreach (MMInput.IMButton button in ButtonList)
            {
                if (button.State.CurrentState == MMInput.ButtonStates.ButtonDown) { button.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

                if (button.State.CurrentState == MMInput.ButtonStates.ButtonUp) { button.State.ChangeState(MMInput.ButtonStates.Off); }
            }
        }


        /// Called every frame, if not on mobile, gets primary movement values from input
        public virtual void SetMovement()
        {
            if (!IsMobile && InputDetectionActive)
            {
                if (SmoothMovement)
                {
                    _primaryMovement.x = Input.GetAxis(_axisHorizontal);
                    _primaryMovement.y = Input.GetAxis(_axisVertical);
                }
                else
                {
                    _primaryMovement.x = Input.GetAxisRaw(_axisHorizontal);
                    _primaryMovement.y = Input.GetAxisRaw(_axisVertical);
                }

                _primaryMovement = ApplyCameraRotation(_primaryMovement);
            }
        }


        /// Called every frame, if not on mobile, gets secondary movement values from input
        public virtual void SetSecondaryMovement()
        {
            if (!IsMobile && InputDetectionActive)
            {
                if (SmoothMovement)
                {
                    _secondaryMovement.x = Input.GetAxis(_axisSecondaryHorizontal);
                    _secondaryMovement.y = Input.GetAxis(_axisSecondaryVertical);
                }
                else
                {
                    _secondaryMovement.x = Input.GetAxisRaw(_axisSecondaryHorizontal);
                    _secondaryMovement.y = Input.GetAxisRaw(_axisSecondaryVertical);
                }

                _secondaryMovement = ApplyCameraRotation(_secondaryMovement);
            }
        }


        /// Called every frame, if not on mobile, gets shoot axis values from input
        protected virtual void SetShootAxis()
        {
            if (!IsMobile && InputDetectionActive)
            {
                ShootAxis = MMInput.ProcessAxisAsButton(_axisShoot, Threshold.y, ShootAxis);
                SecondaryShootAxis = MMInput.ProcessAxisAsButton(_axisShootSecondary, Threshold.y, SecondaryShootAxis, MMInput.AxisTypes.Positive);
            }
        }


        /// Grabs camera rotation input and stores it
        protected virtual void SetCameraRotationAxis()
        {
            if (!IsMobile) { _cameraRotationInput = Input.GetAxis(_axisCamera); }
        }


        /// If you're using a touch joystick, bind your main joystick to this method
        /// <param name="movement">Movement.</param>
        public virtual void SetMovement(Vector2 movement)
        {
            if (IsMobile && InputDetectionActive)
            {
                _primaryMovement.x = movement.x;
                _primaryMovement.y = movement.y;
            }

            _primaryMovement = ApplyCameraRotation(_primaryMovement);
        }


        /// This method lets you bind a mobile joystick to camera rotation
        /// <param name="movement"></param>
        public virtual void SetCameraRotation(Vector2 movement)
        {
            if (IsMobile && InputDetectionActive) { _cameraRotationInput = movement.x; }
        }


        /// If you're using a touch joystick, bind your secondary joystick to this method
        /// <param name="movement">Movement.</param>
        public virtual void SetSecondaryMovement(Vector2 movement)
        {
            if (IsMobile && InputDetectionActive)
            {
                _secondaryMovement.x = movement.x;
                _secondaryMovement.y = movement.y;
            }

            _secondaryMovement = ApplyCameraRotation(_secondaryMovement);
        }


        /// If you're using touch arrows, bind your left/right arrows to this method
        /// <param name="">.</param>
        public virtual void SetHorizontalMovement(float horizontalInput)
        {
            if (IsMobile && InputDetectionActive) { _primaryMovement.x = horizontalInput; }
        }


        /// If you're using touch arrows, bind your secondary down/up arrows to this method
        /// <param name="">.</param>
        public virtual void SetVerticalMovement(float verticalInput)
        {
            if (IsMobile && InputDetectionActive) { _primaryMovement.y = verticalInput; }
        }


        /// If you're using touch arrows, bind your secondary left/right arrows to this method
        /// <param name="">.</param>
        public virtual void SetSecondaryHorizontalMovement(float horizontalInput)
        {
            if (IsMobile && InputDetectionActive) { _secondaryMovement.x = horizontalInput; }
        }


        /// If you're using touch arrows, bind your down/up arrows to this method
        /// <param name="">.</param>
        public virtual void SetSecondaryVerticalMovement(float verticalInput)
        {
            if (IsMobile && InputDetectionActive) { _secondaryMovement.y = verticalInput; }
        }


        /// Sets an associated camera, used to rotate input based on camera position
        /// <param name="targetCamera"></param>
        /// <param name="rotationAxis"></param>
        public virtual void SetCamera(Camera targetCamera, bool camera3D)
        {
            _targetCamera = targetCamera;
            _camera3D = camera3D;
        }


        /// Sets the current camera rotation input, which you'll want to keep between -1 (left) and 1 (right), 0 being no rotation
        /// <param name="newValue"></param>
        public virtual void SetCameraRotationInput(float newValue) { _cameraRotationInput = newValue; }


        /// Rotates input based on camera orientation
        /// <param name="input"></param>
        /// <returns></returns>
        public virtual Vector2 ApplyCameraRotation(Vector2 input)
        {
            if (!InputDetectionActive) { return Vector2.zero; }

            if (RotateInputBasedOnCameraDirection)
            {
                if (_camera3D)
                {
                    _cameraAngle = _targetCamera.transform.localEulerAngles.y;
                    return MMMaths.RotateVector2(input, -_cameraAngle);
                }
                else
                {
                    _cameraAngle = _targetCamera.transform.localEulerAngles.z;
                    return MMMaths.RotateVector2(input, _cameraAngle);
                }
            }
            else { return input; }
        }


        /// If we lose focus, we reset the states of all buttons
        /// <param name="hasFocus"></param>
        protected void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && ResetButtonStatesOnFocusLoss && (ButtonList != null)) { ForceAllButtonStatesTo(MMInput.ButtonStates.ButtonUp); }
        }


        /// Lets you force the state of all buttons in the InputManager to the one specified in parameters
        /// <param name="newState"></param>
        public virtual void ForceAllButtonStatesTo(MMInput.ButtonStates newState)
        {
            foreach (MMInput.IMButton button in ButtonList) { button.State.ChangeState(newState); }
        }

        public virtual void JumpButtonDown() { JumpButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void JumpButtonPressed() { JumpButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void JumpButtonUp() { JumpButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void DashButtonDown() { DashButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void DashButtonPressed() { DashButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void DashButtonUp() { DashButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void CrouchButtonDown() { CrouchButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void CrouchButtonPressed() { CrouchButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void CrouchButtonUp() { CrouchButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void RunButtonDown() { RunButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void RunButtonPressed() { RunButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void RunButtonUp() { RunButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void ReloadButtonDown() { ReloadButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void ReloadButtonPressed() { ReloadButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void ReloadButtonUp() { ReloadButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void InteractButtonDown() { InteractButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void InteractButtonPressed() { InteractButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void InteractButtonUp() { InteractButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void ShootButtonDown() { ShootButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void ShootButtonPressed() { ShootButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void ShootButtonUp() { ShootButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void SecondaryShootButtonDown() { SecondaryShootButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void SecondaryShootButtonPressed() { SecondaryShootButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void SecondaryShootButtonUp() { SecondaryShootButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void PauseButtonDown() { PauseButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void PauseButtonPressed() { PauseButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void PauseButtonUp() { PauseButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void TimeControlButtonDown() { TimeControlButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void TimeControlButtonPressed() { TimeControlButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void TimeControlButtonUp() { TimeControlButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void SwitchWeaponButtonDown() { SwitchWeaponButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void SwitchWeaponButtonPressed() { SwitchWeaponButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void SwitchWeaponButtonUp() { SwitchWeaponButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }

        public virtual void SwitchCharacterButtonDown() { SwitchCharacterButton.State.ChangeState(MMInput.ButtonStates.ButtonDown); }

        public virtual void SwitchCharacterButtonPressed() { SwitchCharacterButton.State.ChangeState(MMInput.ButtonStates.ButtonPressed); }

        public virtual void SwitchCharacterButtonUp() { SwitchCharacterButton.State.ChangeState(MMInput.ButtonStates.ButtonUp); }
    }
}