
using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Character/Abilities/Enigma Character Handle Weapon")]
    public class EnigmaCharacterHandleWeapon : EnigmaCharacterAbility
    {
        [Title("Weapon")]
        [Tooltip("Weapon to spawn & attach on start")]
        public EnigmaWeapon InitialWeapon;

        [Tooltip("If true, all shots will use this mask instead of weapon defaults")]
        public bool UseTargetLayerMask = false;

        [ReadOnly] public LayerMask TargetLayerMask;
        [ReadOnly] public int       DamageableLayer;

        [Title("Feedbacks")]
        [Tooltip("Played at the character level every time the weapon actually fires")]
        public MMFeedbacks WeaponUseFeedback;

        [Title("Binding")]
        [Tooltip("Where the weapon instance gets parented (defaults to this.transform)")]
        public Transform WeaponAttachment;

        [Tooltip("Auto-bind this animator to the weapon for minimal params")]
        public bool AutomaticallyBindAnimator = true;

        [Title("Input")]
        [Tooltip("Hold to continuously fire. If true, we start firing again as long as button is still down (Weapon.TriggerMode==Auto helps too)")]
        public bool ForceAlwaysShoot = false;

        [Tooltip("If the character is hit, interrupt the current attack (only if weapon is Interruptable)")]
        public bool GettingHitInterruptsAttack = false;

        [Title("Debug")]
        [ReadOnly] public EnigmaWeapon CurrentWeapon;

        public virtual int      HandleWeaponID      => 1;
        public virtual Animator CharacterAnimator   { get; protected set; }
        public virtual EnigmaWeaponAim WeaponAimComponent => _weaponAim;

        public delegate void OnWeaponChangeDelegate();
        public OnWeaponChangeDelegate OnWeaponChange;

        // internals
        protected EnigmaWeaponAim _weaponAim;
        protected int _weaponEquippedParam;
        protected int _weaponEquippedIDParam;

        protected const string EquippedParamName   = "WeaponEquipped";
        protected const string EquippedIDParamName = "WeaponEquippedID";

        protected override void PreInitialization()
        {
            base.PreInitialization();
            if (WeaponAttachment == null) WeaponAttachment = transform;
        }

        protected override void Initialization()
        {
            base.Initialization();
            Setup();
        }

        public virtual void Setup()
        {
            _character       = GetComponent<EnigmaCharacter>();
            CharacterAnimator = _animator;
            if (WeaponAttachment == null) WeaponAttachment = transform;

            if (InitialWeapon != null)
            {
                // Equip fresh instance on start
                ChangeWeapon(InitialWeapon, InitialWeapon.WeaponName, combo:false);
            }
        }

        public override void ProcessAbility()
        {
            base.ProcessAbility();
            HandleCharacterState();
            HandleFeedbacks();
            // input is processed from HandleInput() by base class
        }

        protected virtual void HandleCharacterState()
        {
            if (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal)
                ShootStop();
        }

        protected virtual void HandleFeedbacks()
        {
            if (CurrentWeapon != null &&
                CurrentWeapon.WeaponState.CurrentState == EnigmaWeapon.WeaponStates.WeaponUse)
            {
                WeaponUseFeedback?.PlayFeedbacks();
            }
        }

        public void SetTargetLayerMask(LayerMask layerMask) => TargetLayerMask = layerMask;
        public void SetDamageableLayer(int layer)           => DamageableLayer = layer;

        // ---------- INPUT ----------
        protected override void HandleInput()
        {
            if (!AbilityAuthorized
                || (_condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal)
                || (CurrentWeapon == null))
                return;

            bool inputAuthorized = CurrentWeapon.InputAuthorized;

            if (ForceAlwaysShoot)
                ShootStart();

            if (inputAuthorized &&
                (_inputManager.ShootButton.State.CurrentState == MMInput.ButtonStates.ButtonDown
              || _inputManager.ShootAxis == MMInput.ButtonStates.ButtonDown))
            {
                ShootStart();
            }

            if (inputAuthorized &&
                (_inputManager.ShootButton.State.CurrentState == MMInput.ButtonStates.ButtonUp
              || _inputManager.ShootAxis == MMInput.ButtonStates.ButtonUp))
            {
                ShootStop();
                CurrentWeapon.WeaponInputReleased();
            }
        }

        public virtual void ShootStart()
        {
            if (!AbilityAuthorized
                || CurrentWeapon == null
                || _condition.CurrentState != EnigmaCharacterStates.CharacterConditions.Normal)
                return;

            PlayAbilityStartFeedbacks();
            CurrentWeapon.WeaponInputStart();
        }

        public virtual void ShootStop()
        {
            if (!AbilityAuthorized || CurrentWeapon == null) return;
            // Stop immediately – our trimmed weapon has no reload gate keeping
            CurrentWeapon.WeaponInputStop();
            CurrentWeapon.TurnWeaponOff();
            PlayAbilityStopFeedbacks();
        }

        // ---------- EQUIP / CHANGE ----------
        public virtual void ChangeWeapon(EnigmaWeapon newWeapon, string weaponID, bool combo = false)
        {
            if (CurrentWeapon != null)
            {
                CurrentWeapon.TurnWeaponOff();
                var aim = CurrentWeapon.GetComponent<EnigmaWeaponAim>();
                if (aim != null) aim.RemoveReticle();

                Destroy(CurrentWeapon.gameObject);
            }

            if (newWeapon != null)
            {
                InstantiateWeapon(newWeapon, weaponID);
            }
            else
            {
                CurrentWeapon = null;
            }

            OnWeaponChange?.Invoke();
        }

        protected virtual void InstantiateWeapon(EnigmaWeapon prefab, string weaponID)
        {
            CurrentWeapon = Instantiate(prefab,
                WeaponAttachment.transform.position + prefab.WeaponAttachmentOffset,
                WeaponAttachment.transform.rotation);

            CurrentWeapon.name = prefab.name;
            CurrentWeapon.transform.SetParent(WeaponAttachment, worldPositionStays:false);
            CurrentWeapon.transform.localPosition = prefab.WeaponAttachmentOffset;
            CurrentWeapon.WeaponID = weaponID;
            CurrentWeapon.SetOwner(_character, this);

            _weaponAim = CurrentWeapon.GetComponent<EnigmaWeaponAim>();
            ApplyAimSettings();

            CurrentWeapon.Initialization();
            CurrentWeapon.InitializeComboWeapons();         // no-ops in trimmed weapon
            CurrentWeapon.InitializeAnimatorParameters();   // minimal in trimmed weapon
            InitializeAnimatorParameters();
        }

        protected virtual void ApplyAimSettings()
        {
            if (_weaponAim != null && _weaponAim.enabled)
            {
                if (_character != null && _character.FindAbility<EnigmaCharacterHandleWeapon>()?.AutomaticallyBindAnimator == true)
                {
                    // nothing extra to do here for trimmed setup
                }
                _weaponAim.ApplyAim();
            }
        }

        protected override void InitializeAnimatorParameters()
        {
            if (CharacterAnimator == null) return;

            RegisterAnimatorParameter(EquippedParamName,   AnimatorControllerParameterType.Bool, out _weaponEquippedParam);
            RegisterAnimatorParameter(EquippedIDParamName, AnimatorControllerParameterType.Int,  out _weaponEquippedIDParam);
        }

        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _weaponEquippedParam, (CurrentWeapon != null), _character._animatorParameters, _character.RunAnimatorSanityChecks);

            int id = (CurrentWeapon != null) ? CurrentWeapon.WeaponAnimationID : -1;
            MMAnimatorExtensions.UpdateAnimatorInteger(_animator, _weaponEquippedIDParam, id, _character._animatorParameters, _character.RunAnimatorSanityChecks);
        }

        protected override void OnHit()
        {
            base.OnHit();
            if (GettingHitInterruptsAttack && CurrentWeapon != null)
                CurrentWeapon.Interrupt();
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            ShootStop();
            if (CurrentWeapon != null) ChangeWeapon(null, "");
        }

        protected override void OnRespawn()
        {
            base.OnRespawn();
            Setup();
        }
    }
}
