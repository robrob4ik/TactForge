using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    /// Adds this component on a weapon with a WeaponAutoAim (2D or 3D) and it will automatically shoot at targets after an optional delay
    /// To prevent/stop auto shoot, simply disable this component, and enable it again to resume auto shoot
    public class EnigmaWeaponAutoShoot : MonoBehaviour
    {
        [Title("Auto Shoot")]
        /// the delay (in seconds) between acquiring a target and starting shooting at it
        [Tooltip("The delay (in seconds) between acquiring a target and starting shooting at it")]
        public float DelayBeforeShootAfterAcquiringTarget = 0.1f;

        /// if this is true, the weapon will only auto shoot if its owner is idle 
        [Tooltip("If this is true, the weapon will only auto shoot if its owner is idle")]
        public bool OnlyAutoShootIfOwnerIsIdle = false;

        protected EnigmaWeaponAutoAim _weaponAutoAim;
        protected EnigmaWeapon _weapon;
        protected bool _hasWeaponAndAutoAim;
        protected float _targetAcquiredAt;
        protected Transform _lastTarget;


        /// On Awake we initialize our component
        protected virtual void Start()
        {
            Initialization();
        }


        /// Grabs auto aim and weapon
        protected virtual void Initialization()
        {
            _weaponAutoAim = this.gameObject.GetComponent<EnigmaWeaponAutoAim>();
            _weapon = this.gameObject.GetComponent<EnigmaWeapon>();
            if (_weaponAutoAim == null)
            {
                Debug.LogWarning(this.name + " : the WeaponAutoShoot on this object requires that you add either a WeaponAutoAim2D or WeaponAutoAim3D component to your weapon.");
                return;
            }

            _hasWeaponAndAutoAim = (_weapon != null) && (_weaponAutoAim != null);
        }


        /// A public method you can use to update the cached Weapon
        public virtual void SetCurrentWeapon(EnigmaWeapon newWeapon)
        {
            _weapon = newWeapon;
        }


        /// On Update we handle auto shoot
        protected virtual void LateUpdate()
        {
            HandleAutoShoot();
        }


        /// Returns true if this weapon can autoshoot, false otherwise
        public virtual bool CanAutoShoot()
        {
            if (!_hasWeaponAndAutoAim)
            {
                return false;
            }

            if (OnlyAutoShootIfOwnerIsIdle)
            {
                if (_weapon.Owner.MovementState.CurrentState != EnigmaCharacterStates.MovementStates.Idle)
                {
                    return false;
                }
            }

            return true;
        }


        /// Checks if we have a target for enough time, and shoots if needed
        protected virtual void HandleAutoShoot()
        {
            if (!CanAutoShoot())
            {
                return;
            }

            if (_weaponAutoAim.Target != null)
            {
                if (_lastTarget != _weaponAutoAim.Target)
                {
                    _targetAcquiredAt = Time.time;
                }

                if (Time.time - _targetAcquiredAt >= DelayBeforeShootAfterAcquiringTarget)
                {
                    _weapon.WeaponInputStart();
                }

                _lastTarget = _weaponAutoAim.Target;
            }
        }
    }
}