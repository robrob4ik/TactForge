using UnityEngine;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Combo Weapon")]
    public class EnigmaComboWeapon : MonoBehaviour
    {
        public enum InputModes
        {
            SemiAuto,
            Auto
        }

        [Title("Combo")]
        [Tooltip("Whether or not the combo can be dropped if enough time passes between two consecutive attacks")]
        public bool DroppableCombo = true;

        [Tooltip("The delay after which the combo drops")]
        public float DropComboDelay = 0.5f;
        
        [Tooltip("The input mode for this combo weapon. In Auto mode, you'll want to make sure you've set ContinuousPress:true on your CharacterHandleWeapon ability")]
        public InputModes InputMode = InputModes.SemiAuto;

        [Title("Animation")]
        [Tooltip("The name of the animation parameter to update when a combo is in progress.")]
        public string ComboInProgressAnimationParameter = "ComboInProgress";

        [Title("Debug")]
        [ReadOnly]
        [Tooltip("The list of weapons, set automatically by the class")]
        public EnigmaWeapon[] Weapons;
        
        [ReadOnly] 
        [Tooltip("The reference to the weapon's Owner")]
        public EnigmaCharacterHandleWeapon OwnerCharacterHandleWeapon;
        
        [ReadOnly] 
        [Tooltip("The time spent since the last weapon stopped")]
        public float TimeSinceLastWeaponStopped;
        
        public bool ComboInProgress
        {
            get
            {
                bool comboInProgress = false;
                foreach (EnigmaWeapon weapon in Weapons)
                {
                    if (weapon.WeaponState.CurrentState != EnigmaWeapon.WeaponStates.WeaponIdle)
                    {
                        comboInProgress = true;
                    }
                }

                return comboInProgress;
            }
        }

        protected int _currentWeaponIndex = 0;
        protected EnigmaWeaponAutoShoot _weaponAutoShoot;
        protected bool _countdownActive = false;
        
        protected virtual void Start()
        {
            Initialization();
        }

        public virtual void Initialization()
        {
            Weapons = GetComponents<EnigmaWeapon>();
            _weaponAutoShoot = this.gameObject.GetComponent<EnigmaWeaponAutoShoot>();
            InitializeUnusedWeapons();
        }
        
        protected virtual void Update()
        {
            ResetCombo();
        }
        
        public virtual void ResetCombo()
        {
            if (Weapons.Length > 1)
            {
                if (_countdownActive && DroppableCombo)
                {
                    TimeSinceLastWeaponStopped += Time.deltaTime;
                    if (TimeSinceLastWeaponStopped > DropComboDelay)
                    {
                        _countdownActive = false;

                        _currentWeaponIndex = 0;
                        OwnerCharacterHandleWeapon.CurrentWeapon = Weapons[_currentWeaponIndex];
                        OwnerCharacterHandleWeapon.ChangeWeapon(Weapons[_currentWeaponIndex], Weapons[_currentWeaponIndex].WeaponName, true);
                        if (_weaponAutoShoot != null)
                        {
                            _weaponAutoShoot.SetCurrentWeapon(Weapons[_currentWeaponIndex]);
                        }
                    }
                }
            }
        }
        
        public virtual void WeaponStarted(EnigmaWeapon enigmaWeaponThatStarted)
        {
            _countdownActive = false;
        }
        
        public virtual void WeaponStopped(EnigmaWeapon enigmaWeaponThatStopped)
        {
            ProceedToNextWeapon();
        }

        public virtual void ProceedToNextWeapon()
        {
            OwnerCharacterHandleWeapon = Weapons[_currentWeaponIndex].CharacterHandleWeapon;

            int newIndex = 0;
            if (OwnerCharacterHandleWeapon != null)
            {
                if (Weapons.Length > 1)
                {
                    if (_currentWeaponIndex < Weapons.Length - 1)
                    {
                        newIndex = _currentWeaponIndex + 1;
                    }
                    else
                    {
                        newIndex = 0;
                    }

                    _countdownActive = true;
                    TimeSinceLastWeaponStopped = 0f;

                    _currentWeaponIndex = newIndex;
                    OwnerCharacterHandleWeapon.CurrentWeapon = Weapons[newIndex];
                    OwnerCharacterHandleWeapon.CurrentWeapon.WeaponCurrentlyActive = false;
                    OwnerCharacterHandleWeapon.ChangeWeapon(Weapons[newIndex], Weapons[newIndex].WeaponName, true);
                    OwnerCharacterHandleWeapon.CurrentWeapon.WeaponCurrentlyActive = true;

                    if (_weaponAutoShoot != null)
                    {
                        _weaponAutoShoot.SetCurrentWeapon(Weapons[newIndex]);
                    }
                }
            }
        }
        
        protected virtual void InitializeUnusedWeapons()
        {
            for (int i = 0; i < Weapons.Length; i++)
            {
                if (i != _currentWeaponIndex)
                {
                    Weapons[i].SetOwner(Weapons[_currentWeaponIndex].Owner, Weapons[_currentWeaponIndex].CharacterHandleWeapon);
                    Weapons[i].Initialization();
                    Weapons[i].WeaponCurrentlyActive = false;
                }
            }
        }
    }
}