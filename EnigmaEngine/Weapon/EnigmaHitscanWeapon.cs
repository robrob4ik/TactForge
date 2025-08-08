using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace OneBitRob.EnigmaEngine
{
    public class EnigmaHitscanWeapon : EnigmaWeapon
    {
      [FoldoutGroup("Hitscan Spawn"), Title("Hitscan Spawn")]
        [Tooltip("The offset position at which the projectile will spawn")]
        public Vector3 ProjectileSpawnOffset = Vector3.zero;

        [FoldoutGroup("Hitscan Spawn")]
        [Tooltip("The spread (in degrees) to apply randomly (or not) on each angle when spawning a projectile")]
        public Vector3 Spread = Vector3.zero;

        [FoldoutGroup("Hitscan Spawn")]
        [Tooltip("Whether or not the weapon should rotate to align with the spread angle")]
        public bool RotateWeaponOnSpread = false;

        [FoldoutGroup("Hitscan Spawn")]
        [Tooltip("Whether or not the spread should be random (if not it'll be equally distributed)")]
        public bool RandomSpread = true;

        [FoldoutGroup("Hitscan Spawn")]
        [ReadOnly]
        [Tooltip("The projectile's spawn position")]
        public Vector3 SpawnPosition = Vector3.zero;

        [FoldoutGroup("Hitscan"), Title("Hitscan")]
        [Tooltip("The layer(s) on which to hitscan ray should collide")]
        public LayerMask HitscanTargetLayers;

        [FoldoutGroup("Hitscan")]
        [Tooltip("The maximum distance of this weapon, after that bullets will be considered lost")]
        public float HitscanMaxDistance = 100f;

        [FoldoutGroup("Hitscan")]
        [FormerlySerializedAs("DamageCaused")]
        [Tooltip("The minimum amount of damage to apply to a damageable (something with a Health component) every time there's a hit")]
        public float MinDamageCaused = 5;

        [FoldoutGroup("Hitscan")]
        [Tooltip("The maximum amount of damage to apply to a damageable (something with a Health component) every time there's a hit")]
        public float MaxDamageCaused = 5;

        [FoldoutGroup("Hitscan")]
        [Tooltip("The duration of the invincibility after a hit (to prevent insta death in the case of rapid fire)")]
        public float DamageCausedInvincibilityDuration = 0.2f;

        [FoldoutGroup("Hitscan")]
        [Tooltip("A list of typed damage definitions that will be applied on top of the base damage")]
        public List<EnigmaTypedDamage> TypedDamages;

        [FoldoutGroup("Knockback"), Title("Knockback")]
        [Tooltip("The type of knockback to apply when causing damage")]
        public EnigmaDamageOnTouch.KnockbackStyles DamageCausedKnockbackType = EnigmaDamageOnTouch.KnockbackStyles.NoKnockback;

        [FoldoutGroup("Knockback")]
        [Tooltip("The force to apply to the object that gets damaged")]
        public Vector3 DamageCausedKnockbackForce = new Vector3(10, 10, 10);

        [FoldoutGroup("Hit Damageable"), Title("Hit Damageable")]
        [Tooltip("A MMFeedbacks to move to the position of the hit and to play when hitting something with a Health component")]
        public MMFeedbacks HitDamageable;

        [FoldoutGroup("Hit Damageable")]
        [Tooltip("A particle system to move to the position of the hit and to play when hitting something with a Health component")]
        public ParticleSystem DamageableImpactParticles;

        [FoldoutGroup("Hit Non Damageable"), Title("Hit Non Damageable")]
        [Tooltip("A MMFeedbacks to move to the position of the hit and to play when hitting something without a Health component")]
        public MMFeedbacks HitNonDamageable;

        [FoldoutGroup("Hit Non Damageable")]
        [Tooltip("A particle system to move to the position of the hit and to play when hitting something without a Health component")]
        public ParticleSystem NonDamageableImpactParticles;

        protected Vector3 _flippedProjectileSpawnOffset;
        protected Vector3 _randomSpreadDirection;
        protected bool _initialized = false;
        protected Transform _projectileSpawnTransform;
        public virtual RaycastHit _hit { get; protected set; }
        public virtual RaycastHit2D _hit2D { get; protected set; }
        public virtual Vector3 _origin { get; protected set; }
        protected Vector3 _destination;
        protected Vector3 _direction;
        protected GameObject _hitObject = null;
        protected Vector3 _hitPoint;
        protected EnigmaHealth _health;
        protected Vector3 _damageDirection;
        protected Vector3 _knockbackRelativePosition = Vector3.zero;
        protected Vector3 _knockbackForce = Vector3.zero;
        protected EnigmaController _knockbackTopDownController;

        [Button("TestShoot")]
        public bool TestShootButton;
        
        protected virtual void TestShoot()
        {
            if (WeaponState.CurrentState == WeaponStates.WeaponIdle) { WeaponInputStart(); }
            else { WeaponInputStop(); }
        }
        
        public override void Initialization()
        {
            base.Initialization();
            EnigmaWeaponAim = GetComponent<EnigmaWeaponAim>();

            if (!_initialized) { _initialized = true; }
        }
        
        public override void WeaponUse()
        {
            base.WeaponUse();

            DetermineSpawnPosition();
            DetermineDirection();
            SpawnProjectile(SpawnPosition, true);
            HandleDamage();
        }
        
        protected virtual void DetermineDirection()
        {
            if (RandomSpread)
            {
                _randomSpreadDirection.x = UnityEngine.Random.Range(-Spread.x, Spread.x);
                _randomSpreadDirection.y = UnityEngine.Random.Range(-Spread.y, Spread.y);
                _randomSpreadDirection.z = UnityEngine.Random.Range(-Spread.z, Spread.z);
            }
            else { _randomSpreadDirection = Vector3.zero; }

            Quaternion spread = Quaternion.Euler(_randomSpreadDirection);


            _randomSpreadDirection = spread * transform.forward;

            if (RotateWeaponOnSpread) { this.transform.rotation = this.transform.rotation * spread; }
        }
        
        public virtual void SpawnProjectile(Vector3 spawnPosition, bool triggerObjectActivation = true)
        {
            _hitObject = null;

            // we cast a ray in the direction
            _origin = SpawnPosition;
            _hit = MMDebug.Raycast3D(_origin, _randomSpreadDirection, HitscanMaxDistance, HitscanTargetLayers, Color.red, true);

            // if we've hit something, our destination is the raycast hit
            if (_hit.transform != null)
            {
                _hitObject = _hit.collider.gameObject;
                _hitPoint = _hit.point;
            }
            // otherwise we just draw our laser in front of our weapon 
            else { _hitObject = null; }
        }
        
        protected virtual void HandleDamage()
        {
            if (_hitObject == null) { return; }

            _health = _hitObject.MMGetComponentNoAlloc<EnigmaHealth>();

            if (_health == null)
            {
                // hit non damageable
                if (HitNonDamageable != null)
                {
                    HitNonDamageable.transform.position = _hitPoint;
                    HitNonDamageable.transform.LookAt(this.transform);
                    HitNonDamageable.PlayFeedbacks();
                }

                if (NonDamageableImpactParticles != null)
                {
                    NonDamageableImpactParticles.transform.position = _hitPoint;
                    NonDamageableImpactParticles.transform.LookAt(this.transform);
                    NonDamageableImpactParticles.Play();
                }
            }
            else
            {
                // hit damageable
                _damageDirection = (_hitObject.transform.position - this.transform.position).normalized;

                float randomDamage = UnityEngine.Random.Range(MinDamageCaused, Mathf.Max(MaxDamageCaused, MinDamageCaused));
                _health.Damage(randomDamage, this.gameObject, DamageCausedInvincibilityDuration, DamageCausedInvincibilityDuration, _damageDirection, TypedDamages);

                if (HitDamageable != null)
                {
                    HitDamageable.transform.position = _hitPoint;
                    HitDamageable.transform.LookAt(this.transform);
                    HitDamageable.PlayFeedbacks();
                }

                if (DamageableImpactParticles != null)
                {
                    DamageableImpactParticles.transform.position = _hitPoint;
                    DamageableImpactParticles.transform.LookAt(this.transform);
                    DamageableImpactParticles.Play();
                }

                ApplyKnockback();
            }
        }

        protected virtual void ApplyKnockback()
        {
            if (DamageCausedKnockbackType == EnigmaDamageOnTouch.KnockbackStyles.AddForce)
            {
                _knockbackTopDownController = _hitObject.MMGetComponentNoAlloc<EnigmaController>();
                if (_knockbackTopDownController == null) { return; }

                _knockbackForce = DamageCausedKnockbackForce * _health.KnockbackForceMultiplier;
                _knockbackForce = _health.ComputeKnockbackForce(_knockbackForce, TypedDamages);

                _knockbackRelativePosition = _hitPoint - Owner.transform.position;
                _knockbackForce = Quaternion.LookRotation(_knockbackRelativePosition) * _knockbackForce;
                
                _knockbackTopDownController.Impact(_knockbackForce.normalized, _knockbackForce.magnitude);
            }
        }

        public virtual void DetermineSpawnPosition()
        {
            SpawnPosition = this.transform.position + this.transform.rotation * ProjectileSpawnOffset;

            if (WeaponUseTransform != null) { SpawnPosition = WeaponUseTransform.position; }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            DetermineSpawnPosition();

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(SpawnPosition, 0.2f);
        }
    }
}