using UnityEngine;
using Sirenix.OdinInspector;
using MoreMountains.Tools;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Projectile (Trimmed)")]
    public class EnigmaProjectile : MMPoolableObject
    {
        public enum MovementVectors { Forward, Right, Up }

        [Title("Movement"), FoldoutGroup("Movement")]
        public bool FaceDirection = true;

        [FoldoutGroup("Movement")]
        public bool FaceMovement = false;

        [FoldoutGroup("Movement")]
        public MovementVectors MovementVector = MovementVectors.Forward;

        [FoldoutGroup("Movement")]
        [Tooltip("Meters per second")]
        public float Speed = 40f;

        [FoldoutGroup("Movement")]
        [Tooltip("Added to Speed per second")]
        public float Acceleration = 0f;

        [FoldoutGroup("Movement")]
        [Tooltip("Initial move direction if weapon doesn't set one")]
        public Vector3 Direction = Vector3.forward;

        [FoldoutGroup("Spawn"), Title("Spawn")]
        [Tooltip("Initial time during which the projectile won't hurt its owner")]
        public float InitialInvulnerabilityDuration = 0.05f;

        [FoldoutGroup("Damage")]
        [Tooltip("If true, projectile can also damage its owner after invulnerability")]
        public bool DamageOwner = false;

        // runtime
        protected Vector3 _moveDir;
        protected float   _speed0;
        protected float   _life;
        protected float   _invulnTimer;
        protected GameObject _owner;
        protected EnigmaDamageOnTouch _dot;
        protected Rigidbody _rb;

        public virtual EnigmaWeapon SourceWeapon { get; private set; }

        protected virtual void Awake()
        {
            _dot = GetComponent<EnigmaDamageOnTouch>();
            _rb  = GetComponent<Rigidbody>();
            _speed0 = Speed;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _life = 0f;
            _invulnTimer = Mathf.Max(0f, InitialInvulnerabilityDuration);
            Speed = _speed0;

            if (_dot != null && _owner != null)
            {
                _dot.ClearIgnoreList();
                _dot.Owner = _owner;
                if (!DamageOwner)
                    _dot.IgnoreGameObject(_owner);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected virtual void Update()
        {
            // movement
            Speed += Acceleration * Time.deltaTime;
            var step = Mathf.Max(0f, Speed) * Time.deltaTime;

            Vector3 delta = _moveDir * step;

            if (_rb != null && _rb.isKinematic)
                _rb.MovePosition(transform.position + delta);
            else
                transform.position += delta;

            if (FaceMovement)
            {
                switch (MovementVector)
                {
                    case MovementVectors.Forward: transform.forward = _moveDir; break;
                    case MovementVectors.Right:   transform.right   = _moveDir; break;
                    case MovementVectors.Up:      transform.up      = _moveDir; break;
                }
            }

            // invulnerability window counting down
            if (_invulnTimer > 0f)
            {
                _invulnTimer -= Time.deltaTime;
                if (_invulnTimer <= 0f && _dot != null && _owner != null && DamageOwner)
                    _dot.StopIgnoringObject(_owner);
            }
        }

        public virtual void SetDirection(Vector3 newDir, Quaternion newRot, bool faceRight = true)
        {
            _moveDir = newDir.sqrMagnitude < 1e-6f ? Vector3.forward : newDir.normalized;

            if (FaceDirection)
                transform.rotation = newRot;
        }

        public virtual void SetWeapon(EnigmaWeapon weapon)
        {
            SourceWeapon = weapon;
        }

        public virtual void SetOwner(GameObject owner)
        {
            _owner = owner;
            if (_dot != null)
            {
                _dot.Owner = owner;
                _dot.ClearIgnoreList();
                if (!DamageOwner && owner != null) _dot.IgnoreGameObject(owner);
            }
        }

        public virtual void SetLayerMask(LayerMask mask)
        {
            if (_dot != null)
                _dot.TargetLayerMask = mask;
        }

        public virtual void SetDamage(float minDamage, float maxDamage)
        {
            if (_dot != null)
            {
                _dot.MinDamageCaused = minDamage;
                _dot.MaxDamageCaused = maxDamage;
            }
        }

        public virtual GameObject GetOwner() => _owner;

        public virtual void StopAt()
        {
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
            Speed = 0f;
        }
    }
}
