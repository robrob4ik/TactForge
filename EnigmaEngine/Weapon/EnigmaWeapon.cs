using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [SelectionBase]
    public class EnigmaWeapon : MonoBehaviour
    {
        public enum TriggerModes
        {
            SemiAuto,
            Auto
        }

        public enum WeaponStates
        {
            WeaponIdle,
            WeaponStart,
            WeaponDelayBeforeUse,
            WeaponUse,
            WeaponDelayBetweenUses,
            WeaponStop,
            WeaponReloadNeeded,
            WeaponReloadStart,
            WeaponReload,
            WeaponReloadStop,
            WeaponInterrupted
        }

        [Title("ID")]
        [FoldoutGroup("ID")]
        public string WeaponName;

        [FoldoutGroup("ID")]
        [ReadOnly]
        public bool WeaponCurrentlyActive = true;

        [Title("Use")]
        [FoldoutGroup("Use")]
        [Tooltip("The layers that will be damaged by this object")]
        public LayerMask TargetLayerMask;
       
        [FoldoutGroup("Use")]
        public bool InputAuthorized = true;

        [FoldoutGroup("Use")]
        [Tooltip("Is this weapon on semi or full auto ?")]
        public TriggerModes TriggerMode = TriggerModes.Auto;

        [FoldoutGroup("Use")]
        [Tooltip("The delay before use, that will be applied for every shot")]
        public float DelayBeforeUse = 0f;

        [FoldoutGroup("Use")]
        [Tooltip("Whether or not the delay before used can be interrupted by releasing the shoot button (if true, releasing the button will cancel the delayed shot)")]
        public bool DelayBeforeUseReleaseInterruption = true;

        [FoldoutGroup("Use")]
        [Tooltip("The time (in seconds) between two shots")]
        public float TimeBetweenUses = 1f;

        [FoldoutGroup("Use")]
        [Tooltip("Whether or not the time between uses can be interrupted by releasing the shoot button (if true, releasing the button will cancel the time between uses)")]
        public bool TimeBetweenUsesReleaseInterruption = true;

        [Title("Burst Mode")]
        [FoldoutGroup("Burst Mode")]
        [Tooltip("If this is true, the weapon will activate repeatedly for every shoot request")]
        public bool UseBurstMode = false;

        [FoldoutGroup("Burst Mode")]
        [Tooltip("The amount of 'shots' in a burst sequence")]
        public int BurstLength = 3;

        [FoldoutGroup("Burst Mode")]
        [Tooltip("The time between shots in a burst sequence (in seconds)")]
        public float BurstTimeBetweenShots = 0.1f;

        [Title("Magazine")]
        [FoldoutGroup("Magazine")]
        [Tooltip("Whether or not the weapon is magazine based. If it's not, it'll just take its ammo inside a global pool")]
        public bool MagazineBased = false;

        [FoldoutGroup("Magazine")]
        [Tooltip("The size of the magazine")]
        public int MagazineSize = 30;

        [FoldoutGroup("Magazine")]
        [Tooltip("If this is true, pressing the fire button when a reload is needed will reload the weapon. Otherwise you'll need to press the reload button")]
        public bool AutoReload;

        [FoldoutGroup("Magazine")]
        [Tooltip("If this is true, reload will automatically happen right after the last bullet is shot, without the need for input")]
        public bool NoInputReload = false;

        [FoldoutGroup("Magazine")]
        [Tooltip("The time it takes to reload the weapon")]
        public float ReloadTime = 2f;

        [FoldoutGroup("Magazine")]
        [Tooltip("The amount of ammo consumed everytime the weapon fires")]
        public int AmmoConsumedPerShot = 1;

        [FoldoutGroup("Magazine")]
        [Tooltip("If this is set to true, the weapon will auto destroy when there's no ammo left")]
        public bool AutoDestroyWhenEmpty;

        [FoldoutGroup("Magazine")]
        [Tooltip("The delay (in seconds) before weapon destruction if empty")]
        public float AutoDestroyWhenEmptyDelay = 1f;

        [FoldoutGroup("Magazine")]
        [Tooltip("If this is true, the weapon won't try and reload if the ammo is empty, when using WeaponAmmo")]
        public bool PreventReloadIfAmmoEmpty = false;

        [FoldoutGroup("Magazine")]
        [ReadOnly]
        [Tooltip("The current amount of ammo loaded inside the weapon")]
        public int CurrentAmmoLoaded = 0;

        [FoldoutGroup("Position")]
        [Title("Position")]
        [Tooltip("An offset that will be applied to the weapon once attached to the center of the WeaponAttachment transform.")]
        public Vector3 WeaponAttachmentOffset = Vector3.zero;

        [FoldoutGroup("Position")]
        [Tooltip("A transform to use as the spawn point for weapon use (if null, only offset will be considered, otherwise the transform without offset)")]
        public Transform WeaponUseTransform;

        [FoldoutGroup("Recoil")]
        [Title("Recoil")]
        [Tooltip("The force to apply to push the character back when shooting - positive values will push the character back, negative values will launch it forward, turning that recoil into a thrust")]
        public float RecoilForce = 0f;

        [FoldoutGroup("Animation")]
        [Title("Animation")]
        [Tooltip("The other animators (other than the Character's) that you want to update every time this weapon gets used")]
        public List<Animator> Animators;

        [FoldoutGroup("Animation")]
        [Tooltip("If this is true, the weapon's animator(s) will mirror the animation parameter of the owner character (that way your weapon's animator will be able to 'know' if the character is walking, jumping, etc)")]
        public bool MirrorCharacterAnimatorParameters = false;

        [FoldoutGroup("Animation")]
        [Tooltip("The ID of the weapon to pass to the animator")]
        public int WeaponAnimationID = 0;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the weapon's idle animation parameter : this will be true all the time except when the weapon is being used")]
        public string IdleAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the weapon's start animation parameter : true at the frame where the weapon starts being used")]
        public string StartAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the weapon's delay before use animation parameter : true when the weapon has been activated but hasn't been used yet")]
        public string DelayBeforeUseAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the weapon's single use animation parameter : true at each frame the weapon activates (shoots)")]
        public string SingleUseAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the weapon's in use animation parameter : true at each frame the weapon has started firing but hasn't stopped yet")]
        public string UseAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the weapon's delay between each use animation parameter : true when the weapon is in use")]
        public string DelayBetweenUsesAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the weapon stop animation parameter : true after a shot and before the next one or the weapon's stop ")]
        public string StopAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the parameter to send to true as long as this weapon is equipped, used or not. While all the other parameters defined here are updated by the Weapon class itself, and passed to the weapon and character, this one will be updated by CharacterHandleWeapon only.")]
        public string EquippedAnimationParameter;

        [FoldoutGroup("Animation")]
        [Tooltip("The name of the parameter to send to true when the weapon gets interrupted, used or not. While all the other parameters defined here are updated by the Weapon class itself, and passed to the weapon and character, this one will be updated by CharacterHandleWeapon only.")]
        public string InterruptedAnimationParameter;

        [Title("Feedbacks")]
        [FoldoutGroup("Feedbacks")]
      
        [Tooltip("The feedback to play when the weapon starts being used")]
        public MMFeedbacks WeaponStartMMFeedback;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The feedback to play while the weapon is in use")]
        public MMFeedbacks WeaponUsedMMFeedback;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("If set, this feedback will be used randomly instead of WeaponUsedMMFeedback")]
        public MMFeedbacks WeaponUsedMMFeedbackAlt;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The feedback to play when the weapon stops being used")]
        public MMFeedbacks WeaponStopMMFeedback;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The feedback to play when the weapon gets reloaded")]
        public MMFeedbacks WeaponReloadMMFeedback;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The feedback to play when the weapon gets reloaded")]
        public MMFeedbacks WeaponReloadNeededMMFeedback;

        [FoldoutGroup("Feedbacks")]
        [Tooltip("The feedback to play when the weapon can't reload as there's no more ammo available. You'll need PreventReloadIfAmmoEmpty to be true for this to work")]
        public MMFeedbacks WeaponReloadImpossibleMMFeedback;

        [FoldoutGroup("Settings")]
        [FoldoutGroup("Settings")]
        [Tooltip("If this is true, the weapon will initialize itself on start, otherwise it'll have to be init manually, usually by the CharacterHandleWeapon class")]
        public bool InitializeOnStart = false;

        [FoldoutGroup("Settings")]
        [Tooltip("Whether or not this weapon can be interrupted")]
        public bool Interruptable = false;

        public virtual string WeaponID { get; set; }

        public virtual EnigmaCharacter Owner { get; protected set; }

        public virtual EnigmaCharacterHandleWeapon CharacterHandleWeapon { get; set; }

        public virtual EnigmaWeaponAmmo WeaponAmmo { get; protected set; }

        public MMStateMachine<WeaponStates> WeaponState;

        protected EnigmaWeaponAim EnigmaWeaponAim;

        public bool IsComboWeapon { get; set; }
        public bool IsAutoComboWeapon { get; set; }

        protected Animator _ownerAnimator;
        protected EnigmaWeaponPreventShooting _weaponPreventShooting;
        protected float _delayBeforeUseCounter = 0f;
        protected float _delayBetweenUsesCounter = 0f;
        protected float _reloadingCounter = 0f;
        protected bool _triggerReleased = false;
        protected bool _reloading = false;
        protected EnigmaComboWeapon EnigmaComboWeapon;
        protected EnigmaController _controller;
        protected Vector3 _weaponOffset;
        protected Vector3 _weaponAttachmentOffset;
        protected List<HashSet<int>> _animatorParameters;
        protected HashSet<int> _ownerAnimatorParameters;

        protected int _idleAnimationParameter;
        protected int _startAnimationParameter;
        protected int _delayBeforeUseAnimationParameter;
        protected int _singleUseAnimationParameter;
        protected int _useAnimationParameter;
        protected int _delayBetweenUsesAnimationParameter;
        protected int _stopAnimationParameter;

        protected int _comboInProgressAnimationParameter;
        protected int _interruptedAnimationParameter;
        protected int _equippedAnimationParameter;
        protected float _lastShootRequestAt = -float.MaxValue;
        protected float _lastTurnWeaponOnAt = -float.MaxValue;

        protected virtual void Start()
        {
            if (InitializeOnStart) { Initialization(); }
        }

        public virtual void Initialization()
        {
            EnigmaComboWeapon = this.gameObject.GetComponent<EnigmaComboWeapon>();
            _weaponPreventShooting = this.gameObject.GetComponent<EnigmaWeaponPreventShooting>();

            WeaponState = new MMStateMachine<WeaponStates>(gameObject, true);
            WeaponState.ChangeState(WeaponStates.WeaponIdle);
            WeaponAmmo = GetComponent<EnigmaWeaponAmmo>();
            _animatorParameters = new List<HashSet<int>>();
            EnigmaWeaponAim = GetComponent<EnigmaWeaponAim>();
            InitializeAnimatorParameters();
            if (WeaponAmmo == null) { CurrentAmmoLoaded = MagazineSize; }

            InitializeFeedbacks();
        }

        protected virtual void InitializeFeedbacks()
        {
            WeaponStartMMFeedback?.Initialization(this.gameObject);
            WeaponUsedMMFeedback?.Initialization(this.gameObject);
            WeaponUsedMMFeedbackAlt?.Initialization(this.gameObject);
            WeaponStopMMFeedback?.Initialization(this.gameObject);
            WeaponReloadNeededMMFeedback?.Initialization(this.gameObject);
            WeaponReloadMMFeedback?.Initialization(this.gameObject);
        }

        public virtual void InitializeComboWeapons()
        {
            IsComboWeapon = false;
            IsAutoComboWeapon = false;
            if (EnigmaComboWeapon != null)
            {
                IsComboWeapon = true;
                IsAutoComboWeapon = (EnigmaComboWeapon.InputMode == EnigmaComboWeapon.InputModes.Auto);
                EnigmaComboWeapon.Initialization();
            }
        }
        
        public virtual void SetOwner(EnigmaCharacter newOwner, EnigmaCharacterHandleWeapon handleWeapon)
        {
            Owner = newOwner;
            if (Owner != null)
            {
                CharacterHandleWeapon = handleWeapon;
                _controller = Owner.GetComponent<EnigmaController>();

                if (CharacterHandleWeapon.AutomaticallyBindAnimator)
                {
                    if (CharacterHandleWeapon.CharacterAnimator != null) { _ownerAnimator = CharacterHandleWeapon.CharacterAnimator; }

                    if (_ownerAnimator == null)
                    {
                        _ownerAnimator = CharacterHandleWeapon.gameObject.GetComponentInParent<EnigmaCharacter>()
                            .CharacterAnimator;
                    }

                    if (_ownerAnimator == null) { _ownerAnimator = CharacterHandleWeapon.gameObject.GetComponentInParent<Animator>(); }
                }
            }
        }

        public virtual void WeaponInputStart()
        {
            if (_reloading) { return; }

            if (WeaponState.CurrentState == WeaponStates.WeaponIdle)
            {
                _triggerReleased = false;
                TurnWeaponOn();
            }
        }

        public virtual void WeaponInputReleased() { }

        public virtual void TurnWeaponOn()
        {
            if (!InputAuthorized && (Time.time - _lastTurnWeaponOnAt < TimeBetweenUses)) { return; }

            _lastTurnWeaponOnAt = Time.time;

            TriggerWeaponStartFeedback();
            WeaponState.ChangeState(WeaponStates.WeaponStart);

            if (EnigmaComboWeapon != null) { EnigmaComboWeapon.WeaponStarted(this); }
        }

        protected virtual void Update() { ApplyOffset(); }

        protected virtual void LateUpdate() { ProcessWeaponState(); }

        protected virtual void ProcessWeaponState()
        {
            if (WeaponState == null) { return; }

            UpdateAnimator();

            switch (WeaponState.CurrentState)
            {
                case WeaponStates.WeaponIdle:
                    CaseWeaponIdle();
                    break;

                case WeaponStates.WeaponStart:
                    CaseWeaponStart();
                    break;

                case WeaponStates.WeaponDelayBeforeUse:
                    CaseWeaponDelayBeforeUse();
                    break;

                case WeaponStates.WeaponUse:
                    CaseWeaponUse();
                    break;

                case WeaponStates.WeaponDelayBetweenUses:
                    CaseWeaponDelayBetweenUses();
                    break;

                case WeaponStates.WeaponStop:
                    CaseWeaponStop();
                    break;

                case WeaponStates.WeaponReloadNeeded:
                    CaseWeaponReloadNeeded();
                    break;

                case WeaponStates.WeaponReloadStart:
                    CaseWeaponReloadStart();
                    break;

                case WeaponStates.WeaponReload:
                    CaseWeaponReload();
                    break;

                case WeaponStates.WeaponReloadStop:
                    CaseWeaponReloadStop();
                    break;

                case WeaponStates.WeaponInterrupted:
                    CaseWeaponInterrupted();
                    break;
            }
        }

        public virtual void CaseWeaponIdle() { }

        public virtual void CaseWeaponStart()
        {
            if (DelayBeforeUse > 0)
            {
                _delayBeforeUseCounter = DelayBeforeUse;
                WeaponState.ChangeState(WeaponStates.WeaponDelayBeforeUse);
            }
            else { StartCoroutine(ShootRequestCo()); }
        }

        public virtual void CaseWeaponDelayBeforeUse()
        {
            _delayBeforeUseCounter -= Time.deltaTime;
            if (_delayBeforeUseCounter <= 0) { StartCoroutine(ShootRequestCo()); }
        }

        public virtual void CaseWeaponUse()
        {
            WeaponUse();
            _delayBetweenUsesCounter = TimeBetweenUses;
            WeaponState.ChangeState(WeaponStates.WeaponDelayBetweenUses);
        }

        public virtual void CaseWeaponDelayBetweenUses()
        {
            if (_triggerReleased && TimeBetweenUsesReleaseInterruption)
            {
                TurnWeaponOff();
                return;
            }

            _delayBetweenUsesCounter -= Time.deltaTime;
            if (_delayBetweenUsesCounter <= 0)
            {
                if ((TriggerMode == TriggerModes.Auto) && !_triggerReleased) { StartCoroutine(ShootRequestCo()); }
                else { TurnWeaponOff(); }
            }
        }

        public virtual void CaseWeaponStop() { WeaponState.ChangeState(WeaponStates.WeaponIdle); }

        public virtual void CaseWeaponReloadNeeded()
        {
            ReloadNeeded();
            WeaponState.ChangeState(WeaponStates.WeaponIdle);
        }

        public virtual void CaseWeaponReloadStart()
        {
            ReloadWeapon();
            _reloadingCounter = ReloadTime;
            WeaponState.ChangeState(WeaponStates.WeaponReload);
        }

        public virtual void CaseWeaponReload()
        {
            _reloadingCounter -= Time.deltaTime;
            if (_reloadingCounter <= 0) { WeaponState.ChangeState(WeaponStates.WeaponReloadStop); }
        }

        public virtual void CaseWeaponReloadStop()
        {
            _reloading = false;
            WeaponState.ChangeState(WeaponStates.WeaponIdle);
            if (WeaponAmmo == null) { CurrentAmmoLoaded = MagazineSize; }
        }

        public virtual void CaseWeaponInterrupted()
        {
            TurnWeaponOff();
            if ((WeaponState.CurrentState == WeaponStates.WeaponReload)
                || (WeaponState.CurrentState == WeaponStates.WeaponReloadStart)
                || (WeaponState.CurrentState == WeaponStates.WeaponReloadStop)) { return; }

            WeaponState.ChangeState(WeaponStates.WeaponIdle);
        }

        public virtual void Interrupt()
        {
            if ((WeaponState.CurrentState == WeaponStates.WeaponReload)
                || (WeaponState.CurrentState == WeaponStates.WeaponReloadStart)
                || (WeaponState.CurrentState == WeaponStates.WeaponReloadStop)) { return; }

            if (Interruptable) { WeaponState.ChangeState(WeaponStates.WeaponInterrupted); }
        }

        public virtual IEnumerator ShootRequestCo()
        {
            if (Time.time - _lastShootRequestAt < TimeBetweenUses) { yield break; }

            int remainingShots = UseBurstMode ? BurstLength : 1;
            float interval = UseBurstMode ? BurstTimeBetweenShots : 0; // TODO 1;

            while (remainingShots > 0)
            {
                ShootRequest();
                _lastShootRequestAt = Time.time;
                remainingShots--;
                // TODO WHY
                yield return MMCoroutine.WaitFor(interval);
            }
        }

        public virtual void ShootRequest()
        {
            if (_reloading) { return; }

            if (_weaponPreventShooting != null)
            {
                if (!_weaponPreventShooting.ShootingAllowed()) { return; }
            }

            if (MagazineBased)
            {
                if (WeaponAmmo != null)
                {
                    if (WeaponAmmo.EnoughAmmoToFire()) { WeaponState.ChangeState(WeaponStates.WeaponUse); }
                    else
                    {
                        if (AutoReload && MagazineBased) { InitiateReloadWeapon(); }
                        else { WeaponState.ChangeState(WeaponStates.WeaponReloadNeeded); }
                    }
                }
                else
                {
                    if (CurrentAmmoLoaded > 0)
                    {
                        WeaponState.ChangeState(WeaponStates.WeaponUse);
                        CurrentAmmoLoaded -= AmmoConsumedPerShot;
                    }
                    else
                    {
                        if (AutoReload) { InitiateReloadWeapon(); }
                        else { WeaponState.ChangeState(WeaponStates.WeaponReloadNeeded); }
                    }
                }
            }
            else
            {
                if (WeaponAmmo != null)
                {
                    if (WeaponAmmo.EnoughAmmoToFire()) { WeaponState.ChangeState(WeaponStates.WeaponUse); }
                    else { WeaponState.ChangeState(WeaponStates.WeaponReloadNeeded); }
                }
                else { WeaponState.ChangeState(WeaponStates.WeaponUse); }
            }
        }

        public virtual void WeaponUse()
        {
            ApplyRecoil();
            TriggerWeaponUsedFeedback();
        }

        protected virtual void ApplyRecoil()
        {
            if ((RecoilForce != 0f) && (_controller != null))
            {
                if (Owner != null) { _controller.Impact(-this.transform.forward, RecoilForce); }
            }
        }

        public virtual void WeaponInputStop()
        {
            if (_reloading) { return; }

            _triggerReleased = true;
        }

        public virtual void TurnWeaponOff()
        {
            if ((WeaponState.CurrentState == WeaponStates.WeaponIdle ||
                 WeaponState.CurrentState == WeaponStates.WeaponStop)) { return; }

            _triggerReleased = true;

            TriggerWeaponStopFeedback();
            WeaponState.ChangeState(WeaponStates.WeaponStop);
            if (EnigmaComboWeapon != null) { EnigmaComboWeapon.WeaponStopped(this); }

            if (NoInputReload)
            {
                bool needToReload = false;
                if (WeaponAmmo != null) { needToReload = !WeaponAmmo.EnoughAmmoToFire(); }
                else { needToReload = (CurrentAmmoLoaded <= 0); }

                if (needToReload) { InitiateReloadWeapon(); }
            }
        }

        public virtual void ReloadNeeded() { TriggerWeaponReloadNeededFeedback(); }

        public virtual void InitiateReloadWeapon()
        {
            if (PreventReloadIfAmmoEmpty && WeaponAmmo && WeaponAmmo.CurrentAmmoAvailable == 0)
            {
                WeaponReloadImpossibleMMFeedback?.PlayFeedbacks();
                return;
            }

            if (_reloading || !MagazineBased) { return; }

            WeaponState.ChangeState(WeaponStates.WeaponReloadStart);
            _reloading = true;
        }

        protected virtual void ReloadWeapon()
        {
            if (MagazineBased) { TriggerWeaponReloadFeedback(); }
        }

        public virtual IEnumerator WeaponDestruction()
        {
            yield return new WaitForSeconds(AutoDestroyWhenEmptyDelay);

            TurnWeaponOff();
            Destroy(this.gameObject);

            if (WeaponID != null)
            {
                List<int> weaponList = Owner.gameObject.GetComponentInParent<EnigmaCharacter>()
                    ?.FindAbility<EnigmaCharacterInventory>().WeaponInventory.InventoryContains(WeaponID);
                if (weaponList.Count > 0)
                {
                    Owner.gameObject.GetComponentInParent<EnigmaCharacter>()?.FindAbility<EnigmaCharacterInventory>()
                        .WeaponInventory.DestroyItem(weaponList[0]);
                }
            }
        }

        public virtual void ApplyOffset()
        {
            if (!WeaponCurrentlyActive) { return; }

            _weaponAttachmentOffset = WeaponAttachmentOffset;

            if (Owner == null) { return; }


            if (transform.parent != null)
            {
                _weaponOffset = _weaponAttachmentOffset;
                transform.localPosition = _weaponOffset;
            }
        }

        protected virtual void TriggerWeaponStartFeedback() { WeaponStartMMFeedback?.PlayFeedbacks(this.transform.position); }

        protected virtual void TriggerWeaponUsedFeedback()
        {
            if (WeaponUsedMMFeedbackAlt != null)
            {
                int random = MMMaths.RollADice(2);
                if (random > 1) { WeaponUsedMMFeedbackAlt?.PlayFeedbacks(this.transform.position); }
                else { WeaponUsedMMFeedback?.PlayFeedbacks(this.transform.position); }
            }
            else { WeaponUsedMMFeedback?.PlayFeedbacks(this.transform.position); }
        }

        protected virtual void TriggerWeaponStopFeedback() { WeaponStopMMFeedback?.PlayFeedbacks(this.transform.position); }

        protected virtual void TriggerWeaponReloadNeededFeedback() { WeaponReloadNeededMMFeedback?.PlayFeedbacks(this.transform.position); }

        protected virtual void TriggerWeaponReloadFeedback() { WeaponReloadMMFeedback?.PlayFeedbacks(this.transform.position); }

        public virtual void InitializeAnimatorParameters()
        {
            if (Animators.Count > 0)
            {
                for (int i = 0; i < Animators.Count; i++)
                {
                    _animatorParameters.Add(new HashSet<int>());
                    AddParametersToAnimator(Animators[i], _animatorParameters[i]);

                    if (MirrorCharacterAnimatorParameters)
                    {
                        MMAnimatorMirror mirror = Animators[i].gameObject.AddComponent<MMAnimatorMirror>();
                        mirror.SourceAnimator = _ownerAnimator;
                        mirror.TargetAnimator = Animators[i];
                        mirror.Initialization();
                    }
                }
            }

            if (_ownerAnimator != null)
            {
                _ownerAnimatorParameters = new HashSet<int>();
                AddParametersToAnimator(_ownerAnimator, _ownerAnimatorParameters);
            }
        }

        protected virtual void AddParametersToAnimator(Animator animator, HashSet<int> list)
        {
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, EquippedAnimationParameter, out _equippedAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, IdleAnimationParameter, out _idleAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, StartAnimationParameter, out _startAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, DelayBeforeUseAnimationParameter, out _delayBeforeUseAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, DelayBetweenUsesAnimationParameter, out _delayBetweenUsesAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, StopAnimationParameter, out _stopAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, SingleUseAnimationParameter, out _singleUseAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, UseAnimationParameter, out _useAnimationParameter, AnimatorControllerParameterType.Bool, list);
            MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, InterruptedAnimationParameter, out _interruptedAnimationParameter, AnimatorControllerParameterType.Bool, list);

            if (EnigmaComboWeapon != null) { MMAnimatorExtensions.AddAnimatorParameterIfExists(animator, EnigmaComboWeapon.ComboInProgressAnimationParameter, out _comboInProgressAnimationParameter, AnimatorControllerParameterType.Bool, list); }
        }

        public virtual void UpdateAnimator()
        {
            for (int i = 0; i < Animators.Count; i++) { UpdateAnimator(Animators[i], _animatorParameters[i]); }

            if ((_ownerAnimator != null) && (WeaponState != null) && (_ownerAnimatorParameters != null)) { UpdateAnimator(_ownerAnimator, _ownerAnimatorParameters); }
        }

        protected virtual void UpdateAnimator(Animator animator, HashSet<int> list)
        {
            if (_equippedAnimationParameter != 0) MMAnimatorExtensions.UpdateAnimatorBool(animator, _equippedAnimationParameter, true, list);
            if (_idleAnimationParameter != 0) MMAnimatorExtensions.UpdateAnimatorBool(animator, _idleAnimationParameter, (WeaponState.CurrentState == WeaponStates.WeaponIdle), list);
            if (_startAnimationParameter != 0) MMAnimatorExtensions.UpdateAnimatorBool(animator, _startAnimationParameter, (WeaponState.CurrentState == WeaponStates.WeaponStart), list);
            if (_delayBeforeUseAnimationParameter != 0) MMAnimatorExtensions.UpdateAnimatorBool(animator, _delayBeforeUseAnimationParameter, (WeaponState.CurrentState == WeaponStates.WeaponDelayBeforeUse), list);
            if (_useAnimationParameter != 0)
                MMAnimatorExtensions.UpdateAnimatorBool(animator, _useAnimationParameter,
                    (WeaponState.CurrentState == WeaponStates.WeaponDelayBeforeUse || WeaponState.CurrentState == WeaponStates.WeaponUse || WeaponState.CurrentState == WeaponStates.WeaponDelayBetweenUses), list);
            if (_singleUseAnimationParameter != 0) MMAnimatorExtensions.UpdateAnimatorBool(animator, _singleUseAnimationParameter, (WeaponState.CurrentState == WeaponStates.WeaponUse), list);
            if (_delayBetweenUsesAnimationParameter != 0) MMAnimatorExtensions.UpdateAnimatorBool(animator, _delayBetweenUsesAnimationParameter, (WeaponState.CurrentState == WeaponStates.WeaponDelayBetweenUses), list);
            if (_stopAnimationParameter != 0) MMAnimatorExtensions.UpdateAnimatorBool(animator, _stopAnimationParameter, (WeaponState.CurrentState == WeaponStates.WeaponStop), list);

            if (WeaponState.CurrentState == WeaponStates.WeaponInterrupted && _interruptedAnimationParameter != 0) { MMAnimatorExtensions.UpdateAnimatorTrigger(animator, _interruptedAnimationParameter, list); }

            if (EnigmaComboWeapon != null && _comboInProgressAnimationParameter != 0) { MMAnimatorExtensions.UpdateAnimatorBool(animator, _comboInProgressAnimationParameter, EnigmaComboWeapon.ComboInProgress, list); }
        }
    }
}