using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine.Serialization;
using OneBitRob.AI;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime;
using Unity.Entities; // Add this using statement

namespace OneBitRob.EnigmaEngine
{
    public struct HealthChangeEvent
    {
        public EnigmaHealth AffectedEnigmaHealth;
        public float NewHealth;

        public HealthChangeEvent(EnigmaHealth affectedEnigmaHealth, float newHealth)
        {
            AffectedEnigmaHealth = affectedEnigmaHealth;
            NewHealth = newHealth;
        }

        static HealthChangeEvent e;

        public static void Trigger(EnigmaHealth affectedEnigmaHealth, float newHealth)
        {
            e.AffectedEnigmaHealth = affectedEnigmaHealth;
            e.NewHealth = newHealth;
            MMEventManager.TriggerEvent(e);
        }
    }

    public class EnigmaHealth : MonoBehaviour
    {
        [FoldoutGroup("Bindings"), Tooltip("The model GameObject to disable on death if DisableModelOnDeath is true.")]
        public GameObject Model;

        [FoldoutGroup("Status"), ReadOnly, Tooltip("The current health of the character (read-only).")]
        public float CurrentHealth;

        [FoldoutGroup("Status"), ReadOnly, Tooltip("Whether the character is currently invulnerable (read-only).")]
        public bool Invulnerable = false;

        [FoldoutGroup("Health"), Tooltip("The initial health value when the character spawns or resets.")]
        public float InitialHealth = 10;

        [FoldoutGroup("Health"), Tooltip("The maximum health the character can have.")]
        public float MaximumHealth = 10;

        [FoldoutGroup("Health"), Tooltip("Whether to reset health to InitialHealth when the component is enabled.")]
        public bool ResetHealthOnEnable = true;

        [FoldoutGroup("Damage"), Tooltip("Whether the character is immune to all damage.")]
        public bool ImmuneToDamage = false;

        [FoldoutGroup("Damage"), Tooltip("Feedbacks to play when taking damage.")]
        public MMFeedbacks DamageMMFeedbacks;

        [FoldoutGroup("Damage"), Tooltip("Whether the damage feedbacks' intensity is proportional to the damage taken.")]
        public bool FeedbackIsProportionalToDamage = false;

        [FoldoutGroup("Damage"), Tooltip("Whether to prevent the character from damaging itself.")]
        public bool PreventTakeSelfDamage = false;

        [FoldoutGroup("Knockback"), Tooltip("Whether the character is immune to knockback forces.")]
        public bool ImmuneToKnockback = false;

        [FoldoutGroup("Knockback"), Tooltip("Whether to ignore knockback if the damage taken is zero.")]
        public bool ImmuneToKnockbackIfZeroDamage = false;

        [FoldoutGroup("Knockback"), Tooltip("Multiplier applied to incoming knockback forces.")]
        public float KnockbackForceMultiplier = 1f;

        [FoldoutGroup("Death"), Tooltip("Whether to destroy the GameObject on death.")]
        public bool DestroyOnDeath = true;

        [FoldoutGroup("Death"), Tooltip("Whether to respawn at the initial location on revive.")]
        public bool RespawnAtInitialLocation = false;

        [FoldoutGroup("Death"), Tooltip("Whether to disable the controller on death.")]
        public bool DisableControllerOnDeath = true;

        [FoldoutGroup("Death"), Tooltip("Whether to disable the model on death.")]
        public bool DisableModelOnDeath = true;

        [FoldoutGroup("Death"), Tooltip("Whether to disable collisions on death.")]
        public bool DisableCollisionsOnDeath = true;

        [FoldoutGroup("Death"), Tooltip("Whether to disable child collisions on death.")]
        public bool DisableChildCollisionsOnDeath = false;

        [FoldoutGroup("Death"), Tooltip("Whether to change the layer on death.")]
        public bool ChangeLayerOnDeath = false;

        [FoldoutGroup("Death"), Tooltip("Whether to change layers recursively on death.")]
        public bool ChangeLayersRecursivelyOnDeath = false;

        [FoldoutGroup("Death"), Tooltip("The layer to set on death if ChangeLayerOnDeath is true.")]
        public MMLayer LayerOnDeath;

        [FoldoutGroup("Death"), Tooltip("Feedbacks to play on death.")]
        public MMFeedbacks DeathMMFeedbacks;

        [FoldoutGroup("Shared Health and Damage Resistance"), Tooltip("Optional damage resistance processor to handle typed damages.")]
        public EnigmaDamageResistanceProcessor targetEnigmaDamageResistanceProcessor;

        [FoldoutGroup("Animator"), Tooltip("The animator to control for damage and death animations.")]
        public Animator TargetAnimator;

        [FoldoutGroup("Animator"), Tooltip("Whether to disable animator log warnings.")]
        public bool DisableAnimatorLogs = true;

        public virtual float LastDamage { get; set; }
        public virtual Vector3 LastDamageDirection { get; set; }
        public virtual bool Initialized => _initialized;

        public delegate void OnHitDelegate();

        public OnHitDelegate OnHit;

        public delegate void OnReviveDelegate();

        public OnReviveDelegate OnRevive;

        public delegate void OnDeathDelegate();

        public OnDeathDelegate OnDeath;

        protected Vector3 _initialPosition;
        protected Renderer _renderer;
        protected EnigmaCharacter _character;
        protected EnigmaCharacterMovement _characterMovement;
        protected EnigmaController _controller;
        protected EnigmaHealthBar _healthBar;
        protected Collider2D _collider2D;
        protected Collider _collider3D;
        protected CharacterController _characterController;
        protected bool _initialized = false;
        protected int _initialLayer;

        protected const string _deathAnimatorParameterName = "Death";
        protected int _deathAnimatorParameter;

        protected class InterruptiblesDamageOverTimeCoroutine
        {
            public Coroutine DamageOverTimeCoroutine;
            public EnigmaDamageType EnigmaDamageOverTimeType;
        }

        protected List<InterruptiblesDamageOverTimeCoroutine> _damageOverTimeCoroutines;

        protected virtual void Awake()
        {
            Initialization();
            InitializeCurrentHealth();
        }

        protected virtual void Start() { GrabAnimator(); }

        public virtual void Initialization()
        {
            _character = this.gameObject.GetComponentInParent<EnigmaCharacter>();

            if (Model != null) { Model.SetActive(true); }

            if (gameObject.GetComponentInParent<Renderer>() != null) { _renderer = GetComponentInParent<Renderer>(); }

            if (_character != null)
            {
                _characterMovement = _character.FindAbility<EnigmaCharacterMovement>();
                if (_character.CharacterModel != null)
                {
                    if (_character.CharacterModel.GetComponentInChildren<Renderer>() != null) { _renderer = _character.CharacterModel.GetComponentInChildren<Renderer>(); }
                }
            }

            _damageOverTimeCoroutines = new List<InterruptiblesDamageOverTimeCoroutine>();
            _initialLayer = gameObject.layer;

            _deathAnimatorParameter = Animator.StringToHash(_deathAnimatorParameterName);

            _healthBar = this.gameObject.GetComponentInParent<EnigmaHealthBar>();
            _controller = this.gameObject.GetComponentInParent<EnigmaController>();
            _characterController = this.gameObject.GetComponentInParent<CharacterController>();
            _collider2D = this.gameObject.GetComponentInParent<Collider2D>();
            _collider3D = this.gameObject.GetComponentInParent<Collider>();

            DamageMMFeedbacks?.Initialization(this.gameObject);
            DeathMMFeedbacks?.Initialization(this.gameObject);

            StoreInitialPosition();
            _initialized = true;

            DamageEnabled();
        }

        protected virtual void GrabAnimator()
        {
            if (TargetAnimator == null) { BindAnimator(); }

            if ((TargetAnimator != null) && DisableAnimatorLogs) { TargetAnimator.logWarnings = false; }
        }

        protected virtual void BindAnimator()
        {
            if (_character != null)
            {
                if (_character.CharacterAnimator != null) { TargetAnimator = _character.CharacterAnimator; }
                else { TargetAnimator = GetComponent<Animator>(); }
            }
            else { TargetAnimator = GetComponent<Animator>(); }
        }

        public virtual void StoreInitialPosition() { _initialPosition = this.transform.position; }

        public virtual void InitializeCurrentHealth() { SetHealth(InitialHealth); }

        protected virtual void OnEnable()
        {
            if (ResetHealthOnEnable) { InitializeCurrentHealth(); }

            if (Model != null) { Model.SetActive(true); }

            DamageEnabled();
        }

        protected virtual void OnDisable() { }

        public virtual bool CanTakeDamageThisFrame()
        {
            if (Invulnerable || ImmuneToDamage) { return false; }

            if (!this.enabled) { return false; }

            if ((CurrentHealth <= 0) && (InitialHealth != 0)) { return false; }

            return true;
        }

        public virtual void Damage(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<EnigmaTypedDamage> typedDamages = null)
        {
            if (!CanTakeDamageThisFrame()) { return; }

            damage = ComputeDamageOutput(damage, typedDamages, true);

            float previousHealth = CurrentHealth;
            SetHealth(CurrentHealth - damage);

            LastDamage = damage;
            LastDamageDirection = damageDirection;
            if (OnHit != null) { OnHit(); }

            if (invincibilityDuration > 0)
            {
                DamageDisabled();
                StartCoroutine(DamageEnabled(invincibilityDuration));
            }

            EnigmaDamageTakenEvent.Trigger(this, instigator, CurrentHealth, damage, previousHealth, typedDamages);

            if (TargetAnimator != null) { TargetAnimator.SetTrigger("Damage"); }

            if (FeedbackIsProportionalToDamage) { DamageMMFeedbacks?.PlayFeedbacks(this.transform.position, damage); }
            else { DamageMMFeedbacks?.PlayFeedbacks(this.transform.position); }

            UpdateHealthBar();

            ComputeCharacterConditionStateChanges(typedDamages);
            ComputeCharacterMovementMultipliers(typedDamages);

            if (CurrentHealth <= 0)
            {
                CurrentHealth = 0;
                Kill();
            }
        }

        public virtual void StopAllDamageOverTime()
        {
            foreach (InterruptiblesDamageOverTimeCoroutine coroutine in _damageOverTimeCoroutines) { StopCoroutine(coroutine.DamageOverTimeCoroutine); }

            _damageOverTimeCoroutines.Clear();
        }

        public virtual void DamageOverTime(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<EnigmaTypedDamage> typedDamages = null,
            int amountOfRepeats = 0, float durationBetweenRepeats = 1f, bool interruptible = true,
            EnigmaDamageType enigmaDamageType = null)
        {
            if (ComputeDamageOutput(damage, typedDamages, false) == 0) { return; }

            InterruptiblesDamageOverTimeCoroutine damageOverTime = new InterruptiblesDamageOverTimeCoroutine();
            damageOverTime.EnigmaDamageOverTimeType = enigmaDamageType;
            damageOverTime.DamageOverTimeCoroutine = StartCoroutine(DamageOverTimeCo(damage, instigator,
                flickerDuration,
                invincibilityDuration, damageDirection, typedDamages, amountOfRepeats, durationBetweenRepeats,
                interruptible));
            _damageOverTimeCoroutines.Add(damageOverTime);
        }

        protected virtual IEnumerator DamageOverTimeCo(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<EnigmaTypedDamage> typedDamages = null,
            int amountOfRepeats = 0, float durationBetweenRepeats = 1f, bool interruptible = true,
            EnigmaDamageType enigmaDamageType = null)
        {
            for (int i = 0; i < amountOfRepeats; i++)
            {
                Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
                yield return MMCoroutine.WaitFor(durationBetweenRepeats);
            }
        }

        public virtual float ComputeDamageOutput(float damage, List<EnigmaTypedDamage> typedDamages = null,
            bool damageApplied = false)
        {
            if (Invulnerable || ImmuneToDamage) { return 0; }

            float totalDamage = 0f;
            if (targetEnigmaDamageResistanceProcessor != null)
            {
                if (targetEnigmaDamageResistanceProcessor.isActiveAndEnabled)
                {
                    totalDamage =
                        targetEnigmaDamageResistanceProcessor.ProcessDamage(damage, typedDamages, damageApplied);
                }
            }
            else
            {
                totalDamage = damage;
                if (typedDamages != null)
                {
                    foreach (EnigmaTypedDamage typedDamage in typedDamages) { totalDamage += typedDamage.DamageCaused; }
                }
            }

            return totalDamage;
        }

        protected virtual void ComputeCharacterConditionStateChanges(List<EnigmaTypedDamage> typedDamages)
        {
            if ((typedDamages == null) || (_character == null)) { return; }

            foreach (EnigmaTypedDamage typedDamage in typedDamages)
            {
                if (typedDamage.ForceCharacterCondition)
                {
                    if (targetEnigmaDamageResistanceProcessor != null)
                    {
                        if (targetEnigmaDamageResistanceProcessor.isActiveAndEnabled)
                        {
                            bool checkResistance =
                                targetEnigmaDamageResistanceProcessor.CheckPreventCharacterConditionChange(typedDamage
                                    .AssociatedDamageType);
                            if (checkResistance) { continue; }
                        }
                    }

                    _character.ChangeCharacterConditionTemporarily(typedDamage.ForcedCondition,
                        typedDamage.ForcedConditionDuration, typedDamage.ResetControllerForces,
                        typedDamage.DisableGravity);
                }
            }
        }

        protected virtual void ComputeCharacterMovementMultipliers(List<EnigmaTypedDamage> typedDamages)
        {
            if ((typedDamages == null) || (_character == null)) { return; }

            foreach (EnigmaTypedDamage typedDamage in typedDamages)
            {
                if (typedDamage.ApplyMovementMultiplier)
                {
                    if (targetEnigmaDamageResistanceProcessor != null)
                    {
                        if (targetEnigmaDamageResistanceProcessor.isActiveAndEnabled)
                        {
                            bool checkResistance =
                                targetEnigmaDamageResistanceProcessor.CheckPreventMovementModifier(typedDamage
                                    .AssociatedDamageType);
                            if (checkResistance) { continue; }
                        }
                    }

                    _characterMovement?.ApplyMovementMultiplier(typedDamage.MovementMultiplier,
                        typedDamage.MovementMultiplierDuration);
                }
            }
        }

        public virtual Vector3 ComputeKnockbackForce(Vector3 knockbackForce,
            List<EnigmaTypedDamage> typedDamages = null)
        {
            return (targetEnigmaDamageResistanceProcessor == null)
                ? knockbackForce
                : targetEnigmaDamageResistanceProcessor.ProcessKnockbackForce(knockbackForce, typedDamages);
            ;
        }

        public virtual bool CanGetKnockback(List<EnigmaTypedDamage> typedDamages)
        {
            if (ImmuneToKnockback) { return false; }

            if (targetEnigmaDamageResistanceProcessor != null)
            {
                if (targetEnigmaDamageResistanceProcessor.isActiveAndEnabled)
                {
                    bool checkResistance = targetEnigmaDamageResistanceProcessor.CheckPreventKnockback(typedDamages);
                    if (checkResistance) { return false; }
                }
            }

            return true;
        }

        public virtual void Kill()
        {
            if (ImmuneToDamage) { return; }

            if (_character != null)
            {
                _character.ConditionState.ChangeState(EnigmaCharacterStates.CharacterConditions.Dead);
                _character.Reset();

                if (_character.CharacterType == EnigmaCharacter.CharacterTypes.Player) { EnigmaEngineEvent.Trigger(EnigmaEngineEventTypes.PlayerDeath, _character); }
            }

            SetHealth(0);

            StopAllDamageOverTime();
            DamageDisabled();

            DeathMMFeedbacks?.PlayFeedbacks(this.transform.position);

            if (TargetAnimator != null) { TargetAnimator.SetTrigger(_deathAnimatorParameter); }

            if (DisableCollisionsOnDeath)
            {
                if (_collider2D != null) { _collider2D.enabled = false; }

                if (_collider3D != null) { _collider3D.enabled = false; }

                if (_controller != null) { _controller.CollisionsOff(); }

                if (DisableChildCollisionsOnDeath)
                {
                    foreach (Collider2D collider in this.gameObject.GetComponentsInChildren<Collider2D>()) { collider.enabled = false; }

                    foreach (Collider collider in this.gameObject.GetComponentsInChildren<Collider>()) { collider.enabled = false; }
                }
            }

            if (ChangeLayerOnDeath)
            {
                gameObject.layer = LayerOnDeath.LayerIndex;
                if (ChangeLayersRecursivelyOnDeath) { this.transform.ChangeLayersRecursively(LayerOnDeath.LayerIndex); }
            }

            OnDeath?.Invoke();
            EnigmaLifeCycleEvent.Trigger(this, EnigmaLifeCycleEventTypes.Death);

            if (DisableControllerOnDeath && (_controller != null)) { _controller.enabled = false; }

            if (DisableControllerOnDeath && (_characterController != null)) { _characterController.enabled = false; }

            if (DisableModelOnDeath && (Model != null)) { Model.SetActive(false); }

            UnitBrain unitBrain = GetComponent<UnitBrain>();
            if (unitBrain != null)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                var entityManager = world.EntityManager;
                var entity = unitBrain.GetEntity();
                entityManager.AddComponent<DestroyEntityTag>(entity);
                entityManager.SetComponentEnabled<DestroyEntityTag>(entity, true);
            }

            DestroyObject(); // Assuming you've removed the Destroy(toDestroy) line
        }

        public virtual void Revive()
        {
            if (!_initialized) { return; }

            if (_collider2D != null) { _collider2D.enabled = true; }

            if (_collider3D != null) { _collider3D.enabled = true; }

            if (DisableChildCollisionsOnDeath)
            {
                foreach (Collider2D collider in this.gameObject.GetComponentsInChildren<Collider2D>()) { collider.enabled = true; }

                foreach (Collider collider in this.gameObject.GetComponentsInChildren<Collider>()) { collider.enabled = true; }
            }

            if (ChangeLayerOnDeath)
            {
                gameObject.layer = _initialLayer;
                if (ChangeLayersRecursivelyOnDeath) { this.transform.ChangeLayersRecursively(_initialLayer); }
            }

            if (_characterController != null) { _characterController.enabled = true; }

            if (_controller != null)
            {
                _controller.enabled = true;
                _controller.CollisionsOn();
                _controller.Reset();
            }

            if (_character != null) { _character.ConditionState.ChangeState(EnigmaCharacterStates.CharacterConditions.Normal); }

            if (RespawnAtInitialLocation) { transform.position = _initialPosition; }

            if (_healthBar != null) { _healthBar.Initialization(); }

            Initialization();
            InitializeCurrentHealth();
            OnRevive?.Invoke();
            EnigmaLifeCycleEvent.Trigger(this, EnigmaLifeCycleEventTypes.Revive);
        }

        protected virtual void DestroyObject()
        {
            if (_healthBar != null)
            {
                _healthBar.DestroyBar();
            }
        //    GameObject toDestroy = (_character != null) ? _character.gameObject : gameObject;
           // Destroy(toDestroy);
        }

        public virtual void SetHealth(float newValue)
        {
            CurrentHealth = newValue;
            UpdateHealthBar();
            HealthChangeEvent.Trigger(this, newValue);
        }

        public virtual void ReceiveHealth(float health, GameObject instigator)
        {
            SetHealth(Mathf.Min(CurrentHealth + health, MaximumHealth));

            UpdateHealthBar();
        }

        public virtual void ResetHealthToMaxHealth() { SetHealth(MaximumHealth); }

        public virtual void UpdateHealthBar()
        {
            if (_healthBar != null)
            {
                if (CurrentHealth >= MaximumHealth) { _healthBar.ShowBar(false); }
                else { _healthBar.ShowBar(true); }

                _healthBar.UpdateBar(CurrentHealth, 0f, MaximumHealth);
            }

            if (_character != null)
            {
                if (_character.CharacterType == EnigmaCharacter.CharacterTypes.Player)
                {
                    if (EnigmaGUIManager.HasInstance) { EnigmaGUIManager.Instance.UpdateHealthBar(CurrentHealth, 0f, MaximumHealth, _character.PlayerID); }
                }
            }
        }

        public virtual void DamageDisabled() { Invulnerable = true; }

        public virtual void DamageEnabled() { Invulnerable = false; }

        public virtual IEnumerator DamageEnabled(float delay)
        {
            yield return new WaitForSeconds(delay);
            Invulnerable = false;
        }
    }
}