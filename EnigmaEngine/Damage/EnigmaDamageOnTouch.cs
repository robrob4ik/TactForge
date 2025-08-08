using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic;
using System;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Character/Damage/Enigma Damage On Touch")]
    public class EnigmaDamageOnTouch : MonoBehaviour
    {
        [Flags]
        public enum TriggerAndCollisionMask
        {
            IgnoreAll = 0,
            OnTriggerEnter = 1 << 0,
            OnTriggerStay = 1 << 1,

            All_3D = OnTriggerEnter | OnTriggerStay,
        }

        public enum KnockbackStyles
        {
            NoKnockback,
            AddForce
        }

        public enum KnockbackDirections
        {
            BasedOnOwnerPosition,
            BasedOnSpeed,
            BasedOnDirection,
            BasedOnScriptDirection
        }

        public enum DamageDirections
        {
            BasedOnOwnerPosition,
            BasedOnVelocity,
            BasedOnScriptDirection
        }

        public const TriggerAndCollisionMask AllowedTriggerCallbacks = TriggerAndCollisionMask.OnTriggerEnter
                                                                       | TriggerAndCollisionMask.OnTriggerStay;

        [Title("Targets")]
        public LayerMask TargetLayerMask;
      
        [ReadOnly] 
        public GameObject Owner;
        
         public TriggerAndCollisionMask TriggerFilter = AllowedTriggerCallbacks;

        [Title("Damage Caused")]
       public float MinDamageCaused = 10f;
        
        public float MaxDamageCaused = 10f;

        public List<EnigmaTypedDamage> TypedDamages;

        public DamageDirections DamageDirectionMode = DamageDirections.BasedOnVelocity;

        [Title("Knockback")]
        public KnockbackStyles DamageCausedKnockbackType = KnockbackStyles.AddForce;
        
        public KnockbackDirections DamageCausedKnockbackDirection = KnockbackDirections.BasedOnOwnerPosition;

       public Vector3 DamageCausedKnockbackForce = new Vector3(10, 10, 10);

        [Title("Invincibility")]
        public float InvincibilityDuration = 0.5f;

        [Title("Damage over time")]
        public bool RepeatDamageOverTime = false;

        [MMCondition("RepeatDamageOverTime", true)]
        public int AmountOfRepeats = 3;

        [MMCondition("RepeatDamageOverTime", true)]
        public float DurationBetweenRepeats = 1f;
        
        [MMCondition("RepeatDamageOverTime", true)]
        public bool DamageOverTimeInterruptible = true;

        [MMCondition("RepeatDamageOverTime", true)]
        public EnigmaDamageType RepeatedDamageType;

        [Title("Damage Taken")]
        public EnigmaHealth DamageTakenHealth;
        public float DamageTakenEveryTime = 0;
        public float DamageTakenDamageable = 0;
        public float DamageTakenNonDamageable = 0;
        public KnockbackStyles DamageTakenKnockbackType = KnockbackStyles.NoKnockback;
        public Vector3 DamageTakenKnockbackForce = Vector3.zero;
        public float DamageTakenInvincibilityDuration = 0.5f;

        [Title("Feedbacks")]
        public bool HitAnythingFeedbacks = false;
        public MMFeedbacks HitDamageableFeedback;
        public MMFeedbacks HitNonDamageableFeedback;
        public MMFeedbacks HitAnythingFeedback;

        public UnityEvent<EnigmaHealth> HitDamageableEvent;

        public UnityEvent<GameObject> HitNonDamageableEvent;

        public UnityEvent<GameObject> HitAnythingEvent;

        // storage		
        protected Vector3 _lastPosition, _lastDamagePosition, _velocity, _knockbackForce, _damageDirection;
        protected float _startTime = 0f;
        protected EnigmaHealth _colliderHealth;
        protected EnigmaController _topDownController;
        protected EnigmaController _colliderTopDownController;
        protected List<GameObject> _ignoredGameObjects;
        protected Vector3 _knockbackForceApplied;
        protected CircleCollider2D _circleCollider2D;
        protected BoxCollider2D _boxCollider2D;
        protected SphereCollider _sphereCollider;
        protected BoxCollider _boxCollider;
        protected Color _gizmosColor;
        protected Vector3 _gizmoSize;
        protected Vector3 _gizmoOffset;
        protected Transform _gizmoTransform;
        protected bool _twoD = false;
        protected bool _initializedFeedbacks = false;
        protected Vector3 _positionLastFrame;
        protected Vector3 _knockbackScriptDirection;
        protected Vector3 _relativePosition;
        protected Vector3 _damageScriptDirection;
        protected EnigmaHealth _collidingHealth;

        #region Initialization
        protected virtual void Awake()
        {
            Initialization();
        }
        
        protected virtual void OnEnable()
        {
            _startTime = Time.time;
            _lastPosition = transform.position;
            _lastDamagePosition = transform.position;
        }

        public virtual void Initialization()
        {
            InitializeIgnoreList();
            GrabComponents();
            InitalizeGizmos();
            InitializeColliders();
            InitializeFeedbacks();
        }
        
        protected virtual void GrabComponents()
        {
            if (DamageTakenHealth == null)
            {
                DamageTakenHealth = GetComponent<EnigmaHealth>();
            }

            _topDownController = GetComponent<EnigmaController>();
            _boxCollider = GetComponent<BoxCollider>();
            _sphereCollider = GetComponent<SphereCollider>();
            _boxCollider2D = GetComponent<BoxCollider2D>();
            _circleCollider2D = GetComponent<CircleCollider2D>();
            _lastDamagePosition = transform.position;
        }
        
        protected virtual void InitializeColliders()
        {
            _twoD = _boxCollider2D != null || _circleCollider2D != null;
            if (_boxCollider2D != null)
            {
                SetGizmoOffset(_boxCollider2D.offset);
                _boxCollider2D.isTrigger = true;
            }

            if (_boxCollider != null)
            {
                SetGizmoOffset(_boxCollider.center);
                _boxCollider.isTrigger = true;
            }

            if (_sphereCollider != null)
            {
                SetGizmoOffset(_sphereCollider.center);
                _sphereCollider.isTrigger = true;
            }

            if (_circleCollider2D != null)
            {
                SetGizmoOffset(_circleCollider2D.offset);
                _circleCollider2D.isTrigger = true;
            }
        }
        
        protected virtual void InitializeIgnoreList()
        {
            if (_ignoredGameObjects == null) _ignoredGameObjects = new List<GameObject>();
        }

        public virtual void InitializeFeedbacks()
        {
            if (_initializedFeedbacks) return;

            HitDamageableFeedback?.Initialization(this.gameObject);
            HitNonDamageableFeedback?.Initialization(this.gameObject);
            HitAnythingFeedback?.Initialization(this.gameObject);
            _initializedFeedbacks = true;
        }
        
        protected virtual void OnDisable()
        {
            ClearIgnoreList();
        }
        
        protected virtual void OnValidate()
        {
            TriggerFilter &= AllowedTriggerCallbacks;
        }

        #endregion

        #region Gizmos
        protected virtual void InitalizeGizmos()
        {
            _gizmosColor = Color.red;
            _gizmosColor.a = 0.25f;
        }

        public virtual void SetGizmoSize(Vector3 newGizmoSize)
        {
            _boxCollider2D = GetComponent<BoxCollider2D>();
            _boxCollider = GetComponent<BoxCollider>();
            _sphereCollider = GetComponent<SphereCollider>();
            _circleCollider2D = GetComponent<CircleCollider2D>();
            _gizmoSize = newGizmoSize;
        }
        
        public virtual void SetGizmoOffset(Vector3 newOffset)
        {
            _gizmoOffset = newOffset;
        }
        
        protected virtual void OnDrawGizmos()
        {
            Gizmos.color = _gizmosColor;

            if (_boxCollider2D != null)
            {
                if (_boxCollider2D.enabled)
                {
                    MMDebug.DrawGizmoCube(transform, _gizmoOffset, _boxCollider2D.size, false);
                }
                else
                {
                    MMDebug.DrawGizmoCube(transform, _gizmoOffset, _boxCollider2D.size, true);
                }
            }

            if (_circleCollider2D != null)
            {
                Matrix4x4 rotationMatrix = transform.localToWorldMatrix;
                Gizmos.matrix = rotationMatrix;
                if (_circleCollider2D.enabled)
                {
                    Gizmos.DrawSphere((Vector2)_gizmoOffset, _circleCollider2D.radius);
                }
                else
                {
                    Gizmos.DrawWireSphere((Vector2)_gizmoOffset, _circleCollider2D.radius);
                }
            }

            if (_boxCollider != null)
            {
                if (_boxCollider.enabled)
                    MMDebug.DrawGizmoCube(transform,
                        _gizmoOffset,
                        _boxCollider.size,
                        false);
                else
                    MMDebug.DrawGizmoCube(transform,
                        _gizmoOffset,
                        _boxCollider.size,
                        true);
            }

            if (_sphereCollider != null)
            {
                if (_sphereCollider.enabled)
                    Gizmos.DrawSphere(transform.position, _sphereCollider.radius);
                else
                    Gizmos.DrawWireSphere(transform.position, _sphereCollider.radius);
            }
        }

        #endregion

        #region PublicAPIs

        public virtual void SetKnockbackScriptDirection(Vector3 newDirection)
        {
            _knockbackScriptDirection = newDirection;
        }
        
        public virtual void SetDamageScriptDirection(Vector3 newDirection)
        {
            _damageDirection = newDirection;
        }

        public virtual void IgnoreGameObject(GameObject newIgnoredGameObject)
        {
            InitializeIgnoreList();
            _ignoredGameObjects.Add(newIgnoredGameObject);
        }
        
        public virtual void StopIgnoringObject(GameObject ignoredGameObject)
        {
            if (_ignoredGameObjects != null) _ignoredGameObjects.Remove(ignoredGameObject);
        }
        
        public virtual void ClearIgnoreList()
        {
            InitializeIgnoreList();
            _ignoredGameObjects.Clear();
        }

        #endregion

        #region Loop

        protected virtual void Update()
        {
            ComputeVelocity();
        }


        /// On Late Update we store our position
        protected void LateUpdate()
        {
            _positionLastFrame = transform.position;
        }
        
        protected virtual void ComputeVelocity()
        {
            if (Time.deltaTime != 0f)
            {
                _velocity = (_lastPosition - (Vector3)transform.position) / Time.deltaTime;

                if (Vector3.Distance(_lastDamagePosition, transform.position) > 0.5f)
                {
                    _lastDamagePosition = transform.position;
                }

                _lastPosition = transform.position;
            }
        }


        /// Determine the damage direction to pass to the Health Damage method
        protected virtual void DetermineDamageDirection()
        {
            switch (DamageDirectionMode)
            {
                case DamageDirections.BasedOnOwnerPosition:
                    if (Owner == null)
                    {
                        Owner = gameObject;
                    }

                    if (_twoD)
                    {
                        _damageDirection = _collidingHealth.transform.position - Owner.transform.position;
                        _damageDirection.z = 0;
                    }
                    else
                    {
                        _damageDirection = _collidingHealth.transform.position - Owner.transform.position;
                    }

                    break;
                case DamageDirections.BasedOnVelocity:
                    _damageDirection = transform.position - _lastDamagePosition;
                    break;
                case DamageDirections.BasedOnScriptDirection:
                    _damageDirection = _damageScriptDirection;
                    break;
            }

            _damageDirection = _damageDirection.normalized;
        }

        #endregion

        #region CollisionDetection

        public virtual void OnTriggerStay(Collider collider)
        {
            if (0 == (TriggerFilter & TriggerAndCollisionMask.OnTriggerStay)) return;
            Colliding(collider.gameObject);
        }
        
        public virtual void OnTriggerEnter(Collider collider)
        {
            //Debug.Log(Owner.name + " Colliding with " + collider.gameObject.name);
            if (0 == (TriggerFilter & TriggerAndCollisionMask.OnTriggerEnter)) return;
            Colliding(collider.gameObject);
        }

        #endregion

        public void SetTargetLayerMask(LayerMask newTargetLayerMask)
        {
            TargetLayerMask = newTargetLayerMask;
        }
        
        protected virtual void Colliding(GameObject collider)
        {
            if (!EvaluateAvailability(collider))
            {
                return;
            }

            
            _colliderTopDownController = null;
            _colliderHealth = collider.gameObject.MMGetComponentNoAlloc<EnigmaHealth>();
            
            if (_colliderHealth != null)
            {
                if (_colliderHealth.CurrentHealth > 0)
                {
                    OnCollideWithDamageable(_colliderHealth);
                }
            }
            else if (HitAnythingFeedbacks)
            {
                OnCollideWithNonDamageable();
                HitNonDamageableEvent?.Invoke(collider);
                OnAnyCollision(collider);
                HitAnythingEvent?.Invoke(collider);
                HitAnythingFeedback?.PlayFeedbacks(transform.position);
            }
        }
        
        protected virtual bool EvaluateAvailability(GameObject collider)
        {
            // if we're inactive, we do nothing
            if (!isActiveAndEnabled)
            {
                return false;
            }

            // if the object we're colliding with is part of our ignore list, we do nothing and exit
            if (_ignoredGameObjects.Contains(collider))
            {
                return false;
            }

            // if what we're colliding with isn't part of the target layers, we do nothing and exit
            if (!MMLayers.LayerInLayerMask(collider.layer, TargetLayerMask))
            {
                return false;
            }

            // if we're on our first frame, we don't apply damage
            if (Time.time == 0f)
            {
                return false;
            }

            return true;
        }
        
        protected virtual void OnCollideWithDamageable(EnigmaHealth health)
        {
            _collidingHealth = health;

            if (health.CanTakeDamageThisFrame())
            {
                _colliderTopDownController = health.gameObject.MMGetComponentNoAlloc<EnigmaController>();
                if (_colliderTopDownController == null)
                {
                    _colliderTopDownController = health.gameObject.GetComponentInParent<EnigmaController>();
                }

                HitDamageableFeedback?.PlayFeedbacks(this.transform.position);
                HitDamageableEvent?.Invoke(_colliderHealth);

                // we apply the damage to the thing we've collided with
                float randomDamage =
                    UnityEngine.Random.Range(MinDamageCaused, Mathf.Max(MaxDamageCaused, MinDamageCaused));

                ApplyKnockback(randomDamage, TypedDamages);

                DetermineDamageDirection();

                if (RepeatDamageOverTime)
                {
                    _colliderHealth.DamageOverTime(randomDamage, gameObject, InvincibilityDuration,
                        InvincibilityDuration, _damageDirection, TypedDamages, AmountOfRepeats, DurationBetweenRepeats,
                        DamageOverTimeInterruptible, RepeatedDamageType);
                }
                else
                {
                    _colliderHealth.Damage(randomDamage, gameObject, InvincibilityDuration, InvincibilityDuration,
                        _damageDirection, TypedDamages);
                }
            }

            // we apply self damage
            if (DamageTakenEveryTime + DamageTakenDamageable > 0 && !_colliderHealth.PreventTakeSelfDamage)
            {
                SelfDamage(DamageTakenEveryTime + DamageTakenDamageable);
            }
        }

        #region Knockback

        /// Applies knockback if needed
        protected virtual void ApplyKnockback(float damage, List<EnigmaTypedDamage> typedDamages)
        {
            if (ShouldApplyKnockback(damage, typedDamages))
            {
                _knockbackForce = DamageCausedKnockbackForce * _colliderHealth.KnockbackForceMultiplier;
                _knockbackForce = _colliderHealth.ComputeKnockbackForce(_knockbackForce, typedDamages);

                if (_twoD) // if we're in 2D
                {
                    ApplyKnockback2D();
                }
                else // if we're in 3D
                {
                    ApplyKnockback3D();
                }

                if (DamageCausedKnockbackType == KnockbackStyles.AddForce)
                {
                    _colliderTopDownController.Impact(_knockbackForce.normalized, _knockbackForce.magnitude);
                }
            }
        }


        /// Determines whether or not knockback should be applied
        /// <returns></returns>
        protected virtual bool ShouldApplyKnockback(float damage, List<EnigmaTypedDamage> typedDamages)
        {
            if (_colliderHealth.ImmuneToKnockbackIfZeroDamage)
            {
                if (_colliderHealth.ComputeDamageOutput(damage, typedDamages, false) == 0)
                {
                    return false;
                }
            }

            return (_colliderTopDownController != null)
                   && (DamageCausedKnockbackForce != Vector3.zero)
                   && !_colliderHealth.Invulnerable
                   && _colliderHealth.CanGetKnockback(typedDamages);
        }


        /// Applies knockback if we're in a 2D context
        protected virtual void ApplyKnockback2D()
        {
            switch (DamageCausedKnockbackDirection)
            {
                case KnockbackDirections.BasedOnSpeed:
                    var totalVelocity = _colliderTopDownController.Speed + _velocity;
                    _knockbackForce = Vector3.RotateTowards(_knockbackForce,
                        totalVelocity.normalized, 10f, 0f);
                    break;
                case KnockbackDirections.BasedOnOwnerPosition:
                    if (Owner == null)
                    {
                        Owner = gameObject;
                    }

                    _relativePosition = _colliderTopDownController.transform.position - Owner.transform.position;
                    _knockbackForce = Vector3.RotateTowards(_knockbackForce, _relativePosition.normalized, 10f, 0f);
                    break;
                case KnockbackDirections.BasedOnDirection:
                    var direction = transform.position - _positionLastFrame;
                    _knockbackForce = direction * _knockbackForce.magnitude;
                    break;
                case KnockbackDirections.BasedOnScriptDirection:
                    _knockbackForce = _knockbackScriptDirection * _knockbackForce.magnitude;
                    break;
            }
        }


        /// Applies knockback if we're in a 3D context
        protected virtual void ApplyKnockback3D()
        {
            switch (DamageCausedKnockbackDirection)
            {
                case KnockbackDirections.BasedOnSpeed:
                    var totalVelocity = _colliderTopDownController.Speed + _velocity;
                    _knockbackForce = _knockbackForce * totalVelocity.magnitude;
                    break;
                case KnockbackDirections.BasedOnOwnerPosition:
                    if (Owner == null)
                    {
                        Owner = gameObject;
                    }

                    _relativePosition = _colliderTopDownController.transform.position - Owner.transform.position;
                    _knockbackForce = Quaternion.LookRotation(_relativePosition) * _knockbackForce;
                    break;
                case KnockbackDirections.BasedOnDirection:
                    var direction = transform.position - _positionLastFrame;
                    _knockbackForce = direction * _knockbackForce.magnitude;
                    break;
                case KnockbackDirections.BasedOnScriptDirection:
                    _knockbackForce = _knockbackScriptDirection * _knockbackForce.magnitude;
                    break;
            }
        }

        #endregion
        
        protected virtual void OnCollideWithNonDamageable()
        {
            float selfDamage = DamageTakenEveryTime + DamageTakenNonDamageable;
            if (selfDamage > 0)
            {
                SelfDamage(selfDamage);
            }

            HitNonDamageableFeedback?.PlayFeedbacks(transform.position);
        }
        
        protected virtual void OnAnyCollision(GameObject other)
        {
        }
        
        protected virtual void SelfDamage(float damage)
        {
            if (DamageTakenHealth != null)
            {
                _damageDirection = Vector3.up;
                DamageTakenHealth.Damage(damage, gameObject, 0f, DamageTakenInvincibilityDuration, _damageDirection);
            }

            // if what we're colliding with is a TopDownController, we apply a knockback force
            if ((_topDownController != null) && (_colliderTopDownController != null))
            {
                Vector3 totalVelocity = _colliderTopDownController.Speed + _velocity;
                Vector3 knockbackForce =
                    Vector3.RotateTowards(DamageTakenKnockbackForce, totalVelocity.normalized, 10f, 0f);

                if (DamageTakenKnockbackType == KnockbackStyles.AddForce)
                {
                    _topDownController.AddForce(knockbackForce);
                }
            }
        }
    }
}