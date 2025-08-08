using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    [Serializable]
    public class ChargeWeaponStep
    {
        [Tooltip("The weapon to cause an attack with at that step")]
        public EnigmaWeapon TargetWeapon;

        [Tooltip("The duration (in seconds) it should take to keep the charge going to the next step")]
        public float ChargeDuration = 1f;

        [Tooltip("If the charge is interrupted at this step, whether or not to trigger this weapon's attack")]
        public bool TriggerIfChargeInterrupted = true;

        [Tooltip("If this is true, the weapon at this step will be flipped when the charge weapon flips")]
        public bool FlipWhenChargeWeaponFlips = true;

        [Tooltip("A feedback to trigger when this step starts charging")]
        public MMFeedbacks ChargeStartFeedbacks;

        [Tooltip("A feedback to trigger when this step gets interrupted (when the charge is dropped at this step)")]
        public MMFeedbacks ChargeInterruptedFeedbacks;

        [Tooltip("A feedback to trigger when this step completes and the charge potentially moves on to the next step")]
        public MMFeedbacks ChargeCompleteFeedbacks;

        public virtual float ChargeTotalDuration { get; set; }

        public virtual bool ChargeStarted { get; set; }

        public virtual bool ChargeComplete { get; set; }
    }


    [AddComponentMenu("Enigma Engine/Weapons/Enigma Charge Weapon")]
    public class EnigmaChargeWeapon : EnigmaWeapon
    {
        public enum TimescaleModes
        {
            Scaled,
            Unscaled
        }
        
        public enum ReleaseModes
        {
            OnInputRelease,
            AfterLastChargeDuration
        }

        public virtual float DeltaTime => TimescaleMode == TimescaleModes.Scaled ? Time.deltaTime : Time.unscaledDeltaTime;

        public virtual float CurrentTime => TimescaleMode == TimescaleModes.Scaled ? Time.time : Time.unscaledTime;

        [FoldoutGroup("Charge Weapon"), Title("List of Weapons in the Charge Sequence")]
        [Tooltip("The list of weapons that make up this charge weapon's sequence of steps")]
        public List<ChargeWeaponStep> Weapons;

        [FoldoutGroup("Charge Weapon")]
        [Tooltip("Whether this weapon should trigger its attack when all steps are done charging, or when input gets released")]
        public ReleaseModes ReleaseMode = ReleaseModes.OnInputRelease;

        [FoldoutGroup("Charge Weapon")]
        [Tooltip("Whether this weapon's input should run on scaled or unscaled time")]
        public TimescaleModes TimescaleMode = TimescaleModes.Scaled;

        [FoldoutGroup("Charge Weapon")]
        [Tooltip("Whether or not the start of the charge should trigger the first step's weapon's attack or not")]
        public bool AllowInitialShot = true;

        [FoldoutGroup("Charge Weapon")]
        [Title("Debug")]
        [Tooltip("The current charge index in the Weapons step list")]
        [ReadOnly]
        public int CurrentChargeIndex = 0;

        [FoldoutGroup("Charge Weapon")]
        [Tooltip("Whether this weapon is currently charging or not")] 
        [ReadOnly]
        public bool Charging = false;

        protected float _chargingStartedAt = 0f;
        protected int _chargeIndexLastFrame;
        protected int _initialWeaponIndex = 0;
        
        public override void Initialization()
        {
            base.Initialization();
            InitializeTotalDurations();
            InitializeWeapons();
            ResetCharge();
        }
        
        public virtual void InitializeTotalDurations()
        {
            float total = 0f;
            if (DelayBeforeUse > 0)
            {
                total += DelayBeforeUse;
                CurrentChargeIndex = -1;
            }

            foreach (ChargeWeaponStep item in Weapons)
            {
                total += item.ChargeDuration;
                item.ChargeTotalDuration = total;
            }

            _chargeIndexLastFrame = CurrentChargeIndex;
            _initialWeaponIndex = CurrentChargeIndex;
        }
        
        public virtual void ResetCharge()
        {
            Charging = false;
            CurrentChargeIndex = _initialWeaponIndex;
            foreach (ChargeWeaponStep item in Weapons)
            {
                item.ChargeStarted = false;
                item.ChargeComplete = false;
            }
        }
        
        protected virtual void InitializeWeapons()
        {
            foreach (ChargeWeaponStep item in Weapons)
            {
                item.TargetWeapon.SetOwner(Owner, CharacterHandleWeapon);
                item.TargetWeapon.Initialization();
                item.TargetWeapon.InitializeAnimatorParameters();
            }
        }

        protected override void Update()
        {
            base.Update();
            ProcessCharge();
        }
        
        protected virtual void ProcessCharge()
        {
            if (!Charging)
            {
                return;
            }

            CurrentChargeIndex = FindCurrentWeaponIndex();

            if (CurrentChargeIndex != _chargeIndexLastFrame)
            {
                CompleteStepCharge(_chargeIndexLastFrame);
                StartStepCharge(CurrentChargeIndex);
            }

            if ((ReleaseMode == ReleaseModes.AfterLastChargeDuration) && (CurrentChargeIndex == Weapons.Count - 1))
            {
                StopChargeSequence();
            }

            _chargeIndexLastFrame = CurrentChargeIndex;
        }
        
        protected virtual void StartChargeSequence()
        {
            Charging = true;
            _chargingStartedAt = CurrentTime;
            if (WeaponExists(CurrentChargeIndex))
            {
                StartStepCharge(CurrentChargeIndex);
                if (AllowInitialShot)
                {
                    ForceWeaponAttack(0);
                }
            }
        }
        
        protected virtual void StartStepCharge(int index)
        {
            if (!WeaponExists(index))
            {
                return;
            }

            Weapons[index].ChargeStarted = true;
            Weapons[index].ChargeStartFeedbacks?.PlayFeedbacks();
        }
        
        protected virtual void InterruptStepCharge(int index)
        {
            if (!WeaponExists(index))
            {
                return;
            }

            Weapons[index].ChargeStartFeedbacks?.StopFeedbacks();
            Weapons[index].ChargeInterruptedFeedbacks?.PlayFeedbacks();
        }
        
        protected virtual void CompleteStepCharge(int index)
        {
            if (!WeaponExists(index))
            {
                return;
            }

            Weapons[index].ChargeStartFeedbacks?.StopFeedbacks();
            Weapons[index].ChargeComplete = true;
            Weapons[index].ChargeCompleteFeedbacks?.PlayFeedbacks();
        }

        protected virtual void StopChargeSequence()
        {
            if (!Charging)
            {
                return;
            }

            if ((CurrentChargeIndex >= 0) || !AllowInitialShot)
            {
                bool shouldAttack = true;
                if (CurrentChargeIndex < Weapons.Count - 1 && !Weapons[CurrentChargeIndex].ChargeComplete)
                {
                    if (!Weapons[CurrentChargeIndex].TriggerIfChargeInterrupted)
                    {
                        shouldAttack = false;
                    }
                }

                if (shouldAttack)
                {
                    Weapons[CurrentChargeIndex].ChargeStartFeedbacks?.StopFeedbacks();
                    Weapons[CurrentChargeIndex].ChargeCompleteFeedbacks?.StopFeedbacks();
                    if (WeaponExists(CurrentChargeIndex - 1))
                    {
                        Weapons[CurrentChargeIndex - 1].ChargeStartFeedbacks?.StopFeedbacks();
                        Weapons[CurrentChargeIndex - 1].ChargeCompleteFeedbacks?.StopFeedbacks();
                    }

                    ForceWeaponAttack(CurrentChargeIndex);
                }
            }

            if (!Weapons[CurrentChargeIndex].ChargeComplete)
            {
                InterruptStepCharge(CurrentChargeIndex);
            }

            ResetCharge();
        }
        
        protected virtual void ForceWeaponAttack(int index)
        {
            Weapons[index].TargetWeapon.TurnWeaponOn();
        }
        
        protected virtual int FindCurrentWeaponIndex()
        {
            float elapsedTime = CurrentTime - _chargingStartedAt;

            if (elapsedTime < DelayBeforeUse)
            {
                return -1;
            }

            for (int i = 0; i < Weapons.Count; i++)
            {
                if (Weapons[i].ChargeTotalDuration > elapsedTime)
                {
                    return i;
                }
            }

            return Weapons.Count - 1;
        }
        
        protected virtual bool WeaponExists(int index)
        {
            return (index >= 0) && (index < Weapons.Count);
        }
        
        public override void TurnWeaponOn()
        {
            base.TurnWeaponOn();
            StartChargeSequence();
        }
        
        public override void WeaponInputReleased()
        {
            base.WeaponInputReleased();
            StopChargeSequence();
        }
    }
}