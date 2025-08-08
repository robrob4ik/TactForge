using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine.Serialization;

namespace OneBitRob.EnigmaEngine
{
    /// Add this class to a character or object with a Health class, and its health will auto refill based on the settings here
    [AddComponentMenu("Enigma Engine/Character/Health/Enigma Health Auto Refill")]
    public class EnigmaHealthAutoRefill : MonoBehaviour
    {
        /// the possible refill modes :
        /// - linear : constant health refill at a certain rate per second
        /// - bursts : periodic bursts of health
        public enum RefillModes
        {
            Linear,
            Bursts
        }

        [Title("Mode")]
        /// the selected refill mode 
        [Tooltip("The selected refill mode ")]
        public RefillModes RefillMode;

        /// an optional target Health component to refill
        [Tooltip("An optional target Health component to refill")]
        public EnigmaHealth TargetHealth;

        [Title("Cooldown")]
        /// how much time, in seconds, should pass before the refill kicks in
        [Tooltip("how much time, in seconds, should pass before the refill kicks in")]
        public float CooldownAfterHit = 1f;

        [Title("Refill Settings")]
        /// if this is true, health will refill itself when not at full health
        [Tooltip("If this is true, health will refill itself when not at full health")]
        public bool RefillHealth = true;

        /// the amount of health per second to restore when in linear mode
        [MMEnumCondition("RefillMode", (int)RefillModes.Linear)] [Tooltip("The amount of health per second to restore when in linear mode")]
        public float HealthPerSecond;

        /// the amount of health to restore per burst when in burst mode
        [MMEnumCondition("RefillMode", (int)RefillModes.Bursts)] [Tooltip("The amount of health to restore per burst when in burst mode")]
        public float HealthPerBurst = 5;

        /// the duration between two health bursts, in seconds
        [MMEnumCondition("RefillMode", (int)RefillModes.Bursts)] [Tooltip("The duration between two health bursts, in seconds")]
        public float DurationBetweenBursts = 2f;

        protected EnigmaHealth EnigmaHealth;
        protected float _lastHitTime = 0f;
        protected float _healthToGive = 0f;
        protected float _lastBurstTimestamp;


        /// On Awake we do our init
        protected virtual void Awake()
        {
            Initialization();
        }


        /// On init we grab our Health component
        protected virtual void Initialization()
        {
            EnigmaHealth = TargetHealth == null ? this.gameObject.GetComponent<EnigmaHealth>() : TargetHealth;
        }


        /// On Update we refill
        protected virtual void Update()
        {
            ProcessRefillHealth();
        }


        /// Tests if a refill is needed and processes it
        protected virtual void ProcessRefillHealth()
        {
            if (!RefillHealth)
            {
                return;
            }

            if (Time.time - _lastHitTime < CooldownAfterHit)
            {
                return;
            }

            if (EnigmaHealth.CurrentHealth < EnigmaHealth.MaximumHealth)
            {
                switch (RefillMode)
                {
                    case RefillModes.Bursts:
                        if (Time.time - _lastBurstTimestamp > DurationBetweenBursts)
                        {
                            EnigmaHealth.ReceiveHealth(HealthPerBurst, this.gameObject);
                            _lastBurstTimestamp = Time.time;
                        }

                        break;

                    case RefillModes.Linear:
                        _healthToGive += HealthPerSecond * Time.deltaTime;
                        if (_healthToGive > 1f)
                        {
                            float givenHealth = _healthToGive;
                            _healthToGive -= givenHealth;
                            EnigmaHealth.ReceiveHealth(givenHealth, this.gameObject);
                        }

                        break;
                }
            }
        }


        /// On hit we store our time
        public virtual void OnHit()
        {
            _lastHitTime = Time.time;
        }


        /// On enable we start listening for hits
        protected virtual void OnEnable()
        {
            EnigmaHealth.OnHit += OnHit;
        }


        /// On disable we stop listening for hits
        protected virtual void OnDisable()
        {
            EnigmaHealth.OnHit -= OnHit;
        }
    }
}