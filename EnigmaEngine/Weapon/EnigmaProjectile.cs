using System;
using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Projectile")]
    public class EnigmaProjectile : MMPoolableObject
    {
        public enum MovementVectors
        {
            Forward,
            Right,
            Up
        }

        [Title("Movement")]
        [FoldoutGroup("Movement")]
        [Tooltip("If true, the projectile will rotate at initialization towards its rotation")]
        public bool FaceDirection = true;

        [FoldoutGroup("Movement")]
        [Tooltip("If true, the projectile will rotate towards movement")]
        public bool FaceMovement = false;

        [FoldoutGroup("Movement")]
        [ShowIf("FaceMovement")]
        [Tooltip("If FaceMovement is true, the projectile's vector specified below will be aligned to the movement vector, usually you'll want to go with Forward in 3D, Right in 2D")]
        public MovementVectors MovementVector = MovementVectors.Forward;

        [FoldoutGroup("Movement")]
        [Tooltip("The speed of the object (relative to the level's speed)")]
        public float Speed = 0;

        [FoldoutGroup("Movement")]
        [Tooltip("The acceleration of the object over time. Starts accelerating on enable.")]
        public float Acceleration = 0;

        [FoldoutGroup("Movement")]
        [Tooltip("The current direction of the object")]
        public Vector3 Direction = Vector3.left;

        [FoldoutGroup("Movement")]
        [Tooltip("If set to true, the spawner can change the direction of the object. If not the one set in its inspector will be used.")]
        public bool DirectionCanBeChangedBySpawner = true;

        [FoldoutGroup("Movement")]
        [Tooltip("Set this to true if your projectile's model (or sprite) is facing right, false otherwise")]
        public bool ProjectileIsFacingRight = true;

        [FoldoutGroup("Spawn"), Title("Spawn")]
        [Tooltip("The initial delay during which the projectile can't be destroyed")]
        public float InitialInvulnerabilityDuration = 0f;

        [FoldoutGroup("Spawn")]
        [Tooltip("Should the projectile damage its owner?")]
        public bool DamageOwner = false;


        public virtual EnigmaDamageOnTouch targetEnigmaDamageOnTouch => EnigmaDamageOnTouch;

        public virtual EnigmaWeapon sourceEnigmaWeapon => EnigmaWeapon;
        protected EnigmaWeapon EnigmaWeapon;
        protected GameObject _owner;
        protected Vector3 _movement;
        protected float _initialSpeed;
        protected SpriteRenderer _spriteRenderer;
        protected EnigmaDamageOnTouch EnigmaDamageOnTouch;
        protected WaitForSeconds _initialInvulnerabilityDurationWFS;
        protected Collider _collider;
        protected Rigidbody _rigidBody;
        protected bool _facingRightInitially;
        protected bool _initialFlipX;
        protected Vector3 _initialLocalScale;
        protected bool _shouldMove = true;
        protected EnigmaHealth _health;
        protected bool _spawnerIsFacingRight;

        protected virtual void Awake()
        {
            _facingRightInitially = ProjectileIsFacingRight;
            _initialSpeed = Speed;
            _health = GetComponent<EnigmaHealth>();
            _collider = GetComponent<Collider>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            EnigmaDamageOnTouch = GetComponent<EnigmaDamageOnTouch>();
            _rigidBody = GetComponent<Rigidbody>();
            _initialInvulnerabilityDurationWFS = new WaitForSeconds(InitialInvulnerabilityDuration);
            if (_spriteRenderer != null) { _initialFlipX = _spriteRenderer.flipX; }

            _initialLocalScale = transform.localScale;
        }

        protected virtual IEnumerator InitialInvulnerability()
        {
            if (EnigmaDamageOnTouch == null) { yield break; }

            if (EnigmaWeapon == null) { yield break; }

            EnigmaDamageOnTouch.ClearIgnoreList();
            if (EnigmaWeapon.Owner != null) { EnigmaDamageOnTouch.IgnoreGameObject(EnigmaWeapon.Owner.gameObject); }

            yield return _initialInvulnerabilityDurationWFS;
            if (DamageOwner) { EnigmaDamageOnTouch.StopIgnoringObject(EnigmaWeapon.Owner.gameObject); }
        }

        protected virtual void Initialization()
        {
            Speed = _initialSpeed;
            ProjectileIsFacingRight = _facingRightInitially;
            if (_spriteRenderer != null) { _spriteRenderer.flipX = _initialFlipX; }

            transform.localScale = _initialLocalScale;
            _shouldMove = true;
            EnigmaDamageOnTouch?.InitializeFeedbacks();
            EnigmaDamageOnTouch?.SetTargetLayerMask(EnigmaWeapon.CharacterHandleWeapon.TargetLayerMask);

            if (_collider != null) { _collider.enabled = true; }
        }

        protected virtual void FixedUpdate()
        {
            base.Update();
            if (_shouldMove) { Movement(); }
        }

        public virtual void Movement()
        {
            _movement = Direction * (Speed / 10) * Time.deltaTime;

            if (_rigidBody != null) { _rigidBody.MovePosition(this.transform.position + _movement); }
            
            Speed += Acceleration * Time.deltaTime;
        }

        public virtual void SetDirection(Vector3 newDirection, Quaternion newRotation, bool spawnerIsFacingRight = true)
        {
            _spawnerIsFacingRight = spawnerIsFacingRight;

            if (DirectionCanBeChangedBySpawner) { Direction = newDirection; }

            if (FaceDirection) { transform.rotation = newRotation; }

            if (EnigmaDamageOnTouch != null) { EnigmaDamageOnTouch.SetKnockbackScriptDirection(newDirection); }

            if (FaceMovement)
            {
                switch (MovementVector)
                {
                    case MovementVectors.Forward:
                        transform.forward = newDirection;
                        break;
                    case MovementVectors.Right:
                        transform.right = newDirection;
                        break;
                    case MovementVectors.Up:
                        transform.up = newDirection;
                        break;
                }
            }
        }

        public virtual void SetWeapon(EnigmaWeapon newEnigmaWeapon) { EnigmaWeapon = newEnigmaWeapon; }

        public virtual void SetDamage(float minDamage, float maxDamage)
        {
            if (EnigmaDamageOnTouch != null)
            {
                EnigmaDamageOnTouch.MinDamageCaused = minDamage;
                EnigmaDamageOnTouch.MaxDamageCaused = maxDamage;
            }
        }

        public virtual void SetOwner(GameObject newOwner)
        {
            _owner = newOwner;
            EnigmaDamageOnTouch enigmaDamageOnTouch = this.gameObject.MMGetComponentNoAlloc<EnigmaDamageOnTouch>();
            if (enigmaDamageOnTouch != null)
            {
                enigmaDamageOnTouch.Owner = newOwner;
                if (!DamageOwner)
                {
                    enigmaDamageOnTouch.ClearIgnoreList();
                    enigmaDamageOnTouch.IgnoreGameObject(newOwner);
                }
            }
        }

        public virtual GameObject GetOwner() { return _owner; }

        public virtual void StopAt()
        {
            if (_collider != null) { _collider.enabled = false; }
            
            _shouldMove = false;
        }

        protected virtual void OnDeath() { StopAt(); }

        protected override void OnEnable()
        {
            base.OnEnable();

            Initialization();
            if (InitialInvulnerabilityDuration > 0) { StartCoroutine(InitialInvulnerability()); }

            if (_health != null) { _health.OnDeath += OnDeath; }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_health != null) { _health.OnDeath -= OnDeath; }
        }
    }
}