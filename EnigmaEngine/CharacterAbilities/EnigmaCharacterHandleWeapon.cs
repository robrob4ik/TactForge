using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Handle Weapon")]
    public class EnigmaCharacterHandleWeapon : EnigmaCharacterAbility
    {
        [Title("Weapon")]
        [Tooltip("The initial weapon owned by the character")]
        public EnigmaWeapon InitialWeapon;

        [Tooltip("If this is set to true, the character can pick up PickableWeapons")]
        public bool CanPickupWeapons = true;

        [Tooltip("If specified only selected layers would be affected by unit weapons")]
        public bool UseTargetLayerMask = false;

        [Tooltip("Layers that will be affected")]
        [ReadOnly]
        public LayerMask TargetLayerMask;
        [ReadOnly]
        public int DamageableLayer;

        [Title("Feedbacks")]
        [Tooltip("A feedback that gets triggered at the character level everytime the weapon is used")]
        public MMFeedbacks WeaponUseFeedback;

        [Title("Binding")]
        [Tooltip("The position the weapon will be attached to. If left blank, will be this.transform.")]
        public Transform WeaponAttachment;
        
        [Tooltip("If this is true this animator will be automatically bound to the weapon")]
        public bool AutomaticallyBindAnimator = true;

        [Title("Input")]
        [Tooltip("If this is true you won't have to release your fire button to auto reload")]
        public bool ContinuousPress = false;

        [Tooltip("Whether or not this character getting hit should interrupt its attack (will only work if the weapon is marked as interruptable)")]
        public bool GettingHitInterruptsAttack = false;

        [Tooltip("Whether or not pushing the secondary axis above its threshold should cause the weapon to shoot")]
        public bool UseSecondaryAxisThresholdToShoot = false;

        [Tooltip("If this is true, the ForcedWeaponAimControl mode will be applied to all weapons equipped by this character")]
        public bool ForceWeaponAimControl = false;

        [Tooltip("If ForceWeaponAimControl is true, the AimControls mode to apply to all weapons equipped by this character")]
        [MMCondition("ForceWeaponAimControl", true)]
        public EnigmaWeaponAim.AimControls ForcedWeaponAimControl = EnigmaWeaponAim.AimControls.PrimaryMovement;

        [Tooltip("If this is true, the character will continuously fire its weapon")]
        public bool ForceAlwaysShoot = false;

        [Title("Buffering")]
        [Tooltip("Whether or not attack input should be buffered, letting you prepare an attack while another is being performed, making it easier to chain them")]
        public bool BufferInput;

        [MMCondition("BufferInput", true)]
        [Tooltip("If this is true, every new input will prolong the buffer")]
        public bool NewInputExtendsBuffer;

        [MMCondition("BufferInput", true)]
        [Tooltip("The maximum duration for the buffer, in seconds")]
        public float MaximumBufferDuration = 0.25f;

        [MMCondition("BufferInput", true)]
        [Tooltip("If this is true, and if this character is using GridMovement, then input will only be triggered when on a perfect tile")]
        public bool RequiresPerfectTile = false;

        [Title("Debug")]
        [ReadOnly]
        [Tooltip("The weapon currently equipped by the Character")]
        public EnigmaWeapon CurrentWeapon;

        public virtual int HandleWeaponID { get { return 1; } }

        public virtual Animator CharacterAnimator { get; set; }

        public virtual EnigmaWeaponAim WeaponAimComponent { get { return _weaponAim; } }

        public delegate void OnWeaponChangeDelegate();

        public OnWeaponChangeDelegate OnWeaponChange;

        protected EnigmaWeaponAim _weaponAim;
        protected EnigmaProjectileWeapon _projectileWeapon;
        protected float _bufferEndsAt = 0f;
        protected bool _buffering = false;
        protected const string _weaponEquippedAnimationParameterName = "WeaponEquipped";
        protected const string _weaponEquippedIDAnimationParameterName = "WeaponEquippedID";
        protected int _weaponEquippedAnimationParameter;
        protected int _weaponEquippedIDAnimationParameter;
        protected List<EnigmaWeaponModel> _weaponModels;
        
        protected override void PreInitialization()
        {
            base.PreInitialization();
            if (WeaponAttachment == null) { WeaponAttachment = transform; }
        }

        protected override void Initialization()
        {
            base.Initialization();
            Setup();
        }

        public virtual void Setup()
        {
            _character = this.gameObject.GetComponentInParent<EnigmaCharacter>();
            _weaponModels = new List<EnigmaWeaponModel>();
            foreach (EnigmaWeaponModel model in _character.gameObject.GetComponentsInChildren<EnigmaWeaponModel>()) { _weaponModels.Add(model); }

            CharacterAnimator = _animator;
            if (WeaponAttachment == null) { WeaponAttachment = transform; }

            if (InitialWeapon != null)
            {
                if (CurrentWeapon != null)
                {
                    if (CurrentWeapon.name != InitialWeapon.name) { ChangeWeapon(InitialWeapon, InitialWeapon.WeaponName, false); }
                }
                else { ChangeWeapon(InitialWeapon, InitialWeapon.WeaponName, false); }
            }
        }

        public override void ProcessAbility()
        {
            base.ProcessAbility();
            HandleCharacterState();
            HandleFeedbacks();
            HandleBuffer();
        }

        protected virtual void HandleCharacterState()
        {
            if (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal) { ShootStop(); }
        }

        protected virtual void HandleFeedbacks()
        {
            if (CurrentWeapon != null)
            {
                if (CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponUse) { WeaponUseFeedback?.PlayFeedbacks(); }
            }
        }

        public void SetTargetLayerMask(LayerMask layerMask) { TargetLayerMask = layerMask; }
        
        public void SetDamageableLayer(int layer) { DamageableLayer = layer; }
        
        protected override void HandleInput()
        {
            if (!AbilityAuthorized
                || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal)
                || (CurrentWeapon == null)) { return; }

            bool inputAuthorized = true;
            if (CurrentWeapon != null) { inputAuthorized = CurrentWeapon.InputAuthorized; }

            if (ForceAlwaysShoot) { ShootStart(); }

            if (inputAuthorized && ((_inputManager.ShootButton.State.CurrentState == MMInput.ButtonStates.ButtonDown) || (_inputManager.ShootAxis == MMInput.ButtonStates.ButtonDown))) { ShootStart(); }

            bool buttonPressed =
                (_inputManager.ShootButton.State.CurrentState == MMInput.ButtonStates.ButtonPressed) || (_inputManager.ShootAxis == MMInput.ButtonStates.ButtonPressed);

            if (inputAuthorized && ContinuousPress && (CurrentWeapon.TriggerMode == EnigmaWeapon.TriggerModes.Auto) && buttonPressed) { ShootStart(); }

            if (inputAuthorized && ContinuousPress && (CurrentWeapon.IsAutoComboWeapon) && buttonPressed) { ShootStart(); }

            if (_inputManager.ReloadButton.State.CurrentState == MMInput.ButtonStates.ButtonDown) { Reload(); }

            if (inputAuthorized && ((_inputManager.ShootButton.State.CurrentState == MMInput.ButtonStates.ButtonUp) || (_inputManager.ShootAxis == MMInput.ButtonStates.ButtonUp)))
            {
                ShootStop();
                CurrentWeapon.WeaponInputReleased();
            }

            if ((CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponDelayBetweenUses)
                && ((_inputManager.ShootAxis == MMInput.ButtonStates.Off) && (_inputManager.ShootButton.State.CurrentState == MMInput.ButtonStates.Off))
                && !(UseSecondaryAxisThresholdToShoot && (_inputManager.SecondaryMovement.magnitude > _inputManager.Threshold.magnitude))) { CurrentWeapon.WeaponInputStop(); }

            if (inputAuthorized && UseSecondaryAxisThresholdToShoot && (_inputManager.SecondaryMovement.magnitude > _inputManager.Threshold.magnitude)) { ShootStart(); }
        }

        protected virtual void HandleBuffer()
        {
            if (CurrentWeapon == null) { return; }

            if (_buffering && (CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponIdle))
            {
                if (Time.time < _bufferEndsAt) { ShootStart(); }
                else { _buffering = false; }
            }
        }

        public virtual void ShootStart()
        {
            // if the Shoot action is enabled in the permissions, we continue, if not we do nothing.  If the player is dead we do nothing.
            if (!AbilityAuthorized || (CurrentWeapon == null) || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal)) { return; }

            //  if we've decided to buffer input, and if the weapon is in use right now
            if (BufferInput && (CurrentWeapon.WeaponState.CurrentState != EnigmaWeapon.WeaponStates.WeaponIdle))
            {
                // if we're not already buffering, or if each new input extends the buffer, we turn our buffering state to true
                ExtendBuffer();
            }

            PlayAbilityStartFeedbacks();
            CurrentWeapon.WeaponInputStart();
        }

        protected virtual void ExtendBuffer()
        {
            if (!_buffering || NewInputExtendsBuffer)
            {
                _buffering = true;
                _bufferEndsAt = Time.time + MaximumBufferDuration;
            }
        }

        public virtual void ShootStop()
        {
            // if the Shoot action is enabled in the permissions, we continue, if not we do nothing
            if (!AbilityAuthorized || (CurrentWeapon == null)) { return; }

            if (CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponIdle) { return; }

            if ((CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponReload)
                || (CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponReloadStart)
                || (CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponReloadStop)) { return; }

            if ((CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponDelayBeforeUse) &&
                (!CurrentWeapon.DelayBeforeUseReleaseInterruption)) { return; }

            if ((CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponDelayBetweenUses) &&
                (!CurrentWeapon.TimeBetweenUsesReleaseInterruption)) { return; }

            if (CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponUse) { return; }

            ForceStop();
        }

        public virtual void ForceStop()
        {
            StopStartFeedbacks();
            PlayAbilityStopFeedbacks();
            if (CurrentWeapon != null) { CurrentWeapon.TurnWeaponOff(); }
        }

        public virtual void Reload()
        {
            if (CurrentWeapon != null) { CurrentWeapon.InitiateReloadWeapon(); }
        }

        public virtual void ChangeWeapon(EnigmaWeapon newWeapon, string weaponID, bool combo = false)
        {
            if (CurrentWeapon != null)
            {
                CurrentWeapon.TurnWeaponOff();
                if (!combo)
                {
                    ShootStop();

                    if (_weaponAim != null) { _weaponAim.RemoveReticle(); }

                    if (_character._animator != null)
                    {
                        AnimatorControllerParameter[] parameters = _character._animator.parameters;
                        foreach (AnimatorControllerParameter parameter in parameters)
                        {
                            if (parameter.name == CurrentWeapon.EquippedAnimationParameter)
                            {
                                MMAnimatorExtensions.UpdateAnimatorBool(_animator,
                                    CurrentWeapon.EquippedAnimationParameter, false);
                            }
                        }
                    }

                    Destroy(CurrentWeapon.gameObject);
                }
            }

            if (newWeapon != null) { InstantiateWeapon(newWeapon, weaponID, combo); }
            else
            {
                CurrentWeapon = null;
                HandleWeaponModel(null, null);
            }

            if (OnWeaponChange != null) { OnWeaponChange(); }
        }

        protected virtual void InstantiateWeapon(EnigmaWeapon newWeapon, string weaponID, bool combo = false)
        {
            if (!combo)
            {
                CurrentWeapon = (EnigmaWeapon)Instantiate(newWeapon,
                    WeaponAttachment.transform.position + newWeapon.WeaponAttachmentOffset,
                    WeaponAttachment.transform.rotation);
            }

            CurrentWeapon.name = newWeapon.name;
            CurrentWeapon.transform.parent = WeaponAttachment.transform;
            CurrentWeapon.transform.localPosition = newWeapon.WeaponAttachmentOffset;
            CurrentWeapon.SetOwner(_character, this);
            CurrentWeapon.WeaponID = weaponID;
            _weaponAim = CurrentWeapon.gameObject.MMGetComponentNoAlloc<EnigmaWeaponAim>();

            HandleWeaponAim();

            // we handle the weapon model
            HandleWeaponModel(newWeapon, weaponID, combo, CurrentWeapon);

            // we turn off the gun's emitters.
            CurrentWeapon.Initialization();
            CurrentWeapon.InitializeComboWeapons();
            CurrentWeapon.InitializeAnimatorParameters();
            InitializeAnimatorParameters();
        }

        protected virtual void HandleWeaponAim()
        {
            if ((_weaponAim != null) && (_weaponAim.enabled))
            {
                if (ForceWeaponAimControl) { _weaponAim.AimControl = ForcedWeaponAimControl; }

                _weaponAim.ApplyAim();
            }
        }


        protected virtual void HandleWeaponModel(EnigmaWeapon newWeapon, string weaponID, bool combo = false,
            EnigmaWeapon weapon = null)
        {
            if (_weaponModels == null) { return; }

            bool handlesSet = false;

            foreach (EnigmaWeaponModel model in _weaponModels)
            {
                if (model.Owner == this) { model.Hide(); }

                if (model.WeaponID == weaponID)
                {
                    model.Show(this);

                    if (weapon != null)
                    {
                        if (model.BindFeedbacks)
                        {
                            weapon.WeaponStartMMFeedback = model.WeaponStartMMFeedback;
                            weapon.WeaponUsedMMFeedback = model.WeaponUsedMMFeedback;
                            weapon.WeaponStopMMFeedback = model.WeaponStopMMFeedback;
                            weapon.WeaponReloadMMFeedback = model.WeaponReloadMMFeedback;
                            weapon.WeaponReloadNeededMMFeedback = model.WeaponReloadNeededMMFeedback;
                        }

                        if (model.AddAnimator) { weapon.Animators.Add(model.TargetAnimator); }

                        if (model.OverrideWeaponUseTransform) { weapon.WeaponUseTransform = model.WeaponUseTransform; }
                    }
                }
            }
        }

        protected override void InitializeAnimatorParameters()
        {
            if (CurrentWeapon == null) { return; }

            RegisterAnimatorParameter(_weaponEquippedAnimationParameterName, AnimatorControllerParameterType.Bool,
                out _weaponEquippedAnimationParameter);
            RegisterAnimatorParameter(_weaponEquippedIDAnimationParameterName, AnimatorControllerParameterType.Int,
                out _weaponEquippedIDAnimationParameter);
        }

        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _weaponEquippedAnimationParameter,
                (CurrentWeapon != null), _character._animatorParameters, _character.RunAnimatorSanityChecks);
            if (CurrentWeapon == null)
            {
                MMAnimatorExtensions.UpdateAnimatorInteger(_animator, _weaponEquippedIDAnimationParameter, -1,
                    _character._animatorParameters, _character.RunAnimatorSanityChecks);
                return;
            }
            else
            {
                MMAnimatorExtensions.UpdateAnimatorInteger(_animator, _weaponEquippedIDAnimationParameter,
                    CurrentWeapon.WeaponAnimationID, _character._animatorParameters,
                    _character.RunAnimatorSanityChecks);
            }
        }

        protected override void OnHit()
        {
            base.OnHit();
            if (GettingHitInterruptsAttack && (CurrentWeapon != null)) { CurrentWeapon.Interrupt(); }
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            ShootStop();
            if (CurrentWeapon != null) { ChangeWeapon(null, ""); }
        }

        protected override void OnRespawn()
        {
            base.OnRespawn();
            Setup();
        }
    }
}