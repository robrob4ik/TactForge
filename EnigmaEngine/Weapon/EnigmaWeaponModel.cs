using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    /// A class used to force a model to aim at a Weapon's target
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Weapon Model")]
    public class EnigmaWeaponModel : MonoBehaviour
    {
        [Title("Model")]
        /// a unique ID that will be used to hide / show this model when the corresponding weapon gets equipped
        [Tooltip("A unique ID that will be used to hide / show this model when the corresponding weapon gets equipped")]
        public string WeaponID = "WeaponID";

        [Tooltip("A GameObject to show/hide for this model, usually nested right below the logic level of the WeaponModel")]
        public GameObject TargetModel;

        [Title("Aim")] 
        [Tooltip("If this is true, the model will aim at the parent weapon's target")]
        public bool AimWeaponModelAtTarget = true;

        [Tooltip("If this is true, the model's aim will be vertically locked (no up/down aiming)")]
        public bool LockVerticalRotation = true;

        [Title("Animator")]
        /// whether or not to add the target animator to the real weapon's animator list
        [Tooltip("Whether or not to add the target animator to the real weapon's animator list")]
        public bool AddAnimator = false;

        [Tooltip("The animator to send weapon animation parameters to")]
        public Animator TargetAnimator;

        [Title("SpawnTransform")] 
        [Tooltip("Whether or not to override the weapon use transform")]
        public bool OverrideWeaponUseTransform = false;

        [Tooltip("A transform to use as the spawn point for weapon use (if null, only offset will be considered, otherwise the transform without offset)")]
        public Transform WeaponUseTransform;

        [Title("Feedbacks")] 
        [Tooltip("If this is true, the model's feedbacks will replace the original weapon's feedbacks")]
        public bool BindFeedbacks = true;

        [Tooltip("The feedback to play when the weapon starts being used")]
        public MMFeedbacks WeaponStartMMFeedback;

        [Tooltip("The feedback to play while the weapon is in use")]
        public MMFeedbacks WeaponUsedMMFeedback;

        [Tooltip("The feedback to play when the weapon stops being used")]
        public MMFeedbacks WeaponStopMMFeedback;

        [Tooltip("The feedback to play when the weapon gets reloaded")]
        public MMFeedbacks WeaponReloadMMFeedback;

        [Tooltip("The feedback to play when the weapon gets reloaded")]
        public MMFeedbacks WeaponReloadNeededMMFeedback;

        public virtual EnigmaCharacterHandleWeapon Owner { get; set; }

        protected List<EnigmaCharacterHandleWeapon> _handleWeapons;
        protected EnigmaWeaponAim EnigmaWeaponAim;
        protected Vector3 _rotationDirection;

        protected virtual void Awake()
        {
            Hide();
        }


        /// On Start we grab our CharacterHandleWeapon component
        protected virtual void Start()
        {
            _handleWeapons = this.GetComponentInParent<EnigmaCharacter>()?.FindAbilities<EnigmaCharacterHandleWeapon>();
        }


        /// Aims the weapon model at the target
        protected virtual void Update()
        {
            if (!AimWeaponModelAtTarget)
            {
                return;
            }

            if (EnigmaWeaponAim == null)
            {
                foreach (EnigmaCharacterHandleWeapon handleWeapon in _handleWeapons)
                {
                    if (handleWeapon.CurrentWeapon != null)
                    {
                        EnigmaWeaponAim =
                            handleWeapon.CurrentWeapon.gameObject.MMGetComponentNoAlloc<EnigmaWeaponAim>();
                    }
                }
            }
            else
            {
                this.transform.rotation = EnigmaWeaponAim.transform.rotation;
            }
        }

        public virtual void Show(EnigmaCharacterHandleWeapon handleWeapon)
        {
            Owner = handleWeapon;
            TargetModel.SetActive(true);
        }

        public virtual void Hide()
        {
            TargetModel.SetActive(false);
        }
    }
}