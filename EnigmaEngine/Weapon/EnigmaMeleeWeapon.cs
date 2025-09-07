using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Melee Weapon")]
    public class EnigmaMeleeWeapon : EnigmaWeapon
    {
        public enum MeleeDamageAreaShapes { Rectangle, Circle, Box, Sphere }
        public enum MeleeDamageAreaModes  { Generated, Existing }

        [FoldoutGroup("Damage Area"), Title("Damage Area")]
        [Tooltip("Generated = the weapon builds a trigger at runtime; Existing = you bind a prebuilt area (child object)")]
        public MeleeDamageAreaModes MeleeDamageAreaMode = MeleeDamageAreaModes.Generated;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The shape of the damage area")]
        public MeleeDamageAreaShapes DamageAreaShape = MeleeDamageAreaShapes.Rectangle;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("Local offset from this weapon")]
        public Vector3 AreaOffset = new Vector3(1, 0);

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The size of the damage area")]
        public Vector3 AreaSize = new Vector3(1, 1);

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("Trigger filters applied by EnigmaDamageOnTouch")]
        public EnigmaDamageOnTouch.TriggerAndCollisionMask TriggerFilter = EnigmaDamageOnTouch.AllowedTriggerCallbacks;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("Feedback when hitting a Damageable")]
        public MMFeedbacks HitDamageableFeedback;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("Feedback when hitting a non Damageable")]
        public MMFeedbacks HitNonDamageableFeedback;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Existing")]
        [Tooltip("Bind an existing damage area (child)")]
        public EnigmaDamageOnTouch ExistingDamageArea;

        [FoldoutGroup("Damage Area Timing"), Title("Damage Area Timing")]
        [Tooltip("Delay before the damage area becomes active (use to sync with animation)")]
        public float InitialDelay = 0f;

        [FoldoutGroup("Damage Area Timing")]
        [Tooltip("How long the damage area stays active")]
        public float ActiveDuration = 1f;

        [Title("Damage Caused")]
        [FoldoutGroup("Damage Caused")]
        [Tooltip("Min damage")]
        public float MinDamageCaused = 10f;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("Max damage")]
        public float MaxDamageCaused = 10f;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("Knockback style")]
        public EnigmaDamageOnTouch.KnockbackStyles Knockback;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("Knockback force")]
        public Vector3 KnockbackForce = new Vector3(10, 2, 0);

        [FoldoutGroup("Damage Caused")]
        [Tooltip("Knockback direction")]
        public EnigmaDamageOnTouch.KnockbackDirections KnockbackDirection = EnigmaDamageOnTouch.KnockbackDirections.BasedOnOwnerPosition;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("Invincibility frames (seconds) granted to the victim after hit")]
        public float InvincibilityDuration = 0.5f;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("If true, the owner can be damaged by this weapon")]
        public bool CanDamageOwner = false;

        // runtime
        protected Collider _damageAreaCollider;
        protected Collider2D _damageAreaCollider2D;
        protected bool _attackInProgress = false;
        protected CircleCollider2D _circleCollider2D;
        protected BoxCollider2D _boxCollider2D;
        protected BoxCollider _boxCollider;
        protected SphereCollider _sphereCollider;
        protected EnigmaDamageOnTouch DamageOnTouch;
        protected GameObject _damageArea;
        protected Coroutine _attackCoroutine;

        public override void Initialization()
        {
            base.Initialization();

            if (_damageArea == null)
            {
                CreateDamageArea();
                DisableDamageArea();
            }

            if (Owner != null && DamageOnTouch != null)
            {
                DamageOnTouch.Owner = Owner.gameObject;
            }
        }

        protected virtual void CreateDamageArea()
        {
            if ((MeleeDamageAreaMode == MeleeDamageAreaModes.Existing) && (ExistingDamageArea != null))
            {
                _damageArea = ExistingDamageArea.gameObject;
                _damageAreaCollider = _damageArea.GetComponent<Collider>();
                _damageAreaCollider2D = _damageArea.GetComponent<Collider2D>();
                DamageOnTouch = ExistingDamageArea;
                return;
            }

            _damageArea = new GameObject($"{name}DamageArea");
            _damageArea.transform.position = transform.position;
            _damageArea.transform.rotation = transform.rotation;
            _damageArea.transform.SetParent(transform);
            _damageArea.transform.localScale = Vector3.one;
            _damageArea.layer = CharacterHandleWeapon.DamageableLayer;

            // 3D shapes
            if (DamageAreaShape == MeleeDamageAreaShapes.Box)
            {
                _boxCollider = _damageArea.AddComponent<BoxCollider>();
                _boxCollider.center = AreaOffset;
                _boxCollider.size = AreaSize;
                _damageAreaCollider = _boxCollider;
                _damageAreaCollider.isTrigger = true;

                var rb = _damageArea.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.gameObject.AddComponent<MMRagdollerIgnore>();
            }

            if (DamageAreaShape == MeleeDamageAreaShapes.Sphere)
            {
                _sphereCollider = _damageArea.AddComponent<SphereCollider>();
                _sphereCollider.transform.position = transform.position + transform.rotation * AreaOffset;
                _sphereCollider.radius = AreaSize.x * 0.5f;
                _damageAreaCollider = _sphereCollider;
                _damageAreaCollider.isTrigger = true;

                var rb = _damageArea.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.gameObject.AddComponent<MMRagdollerIgnore>();
            }

            DamageOnTouch = _damageArea.AddComponent<EnigmaDamageOnTouch>();
            DamageOnTouch.SetGizmoSize(AreaSize);
            DamageOnTouch.SetGizmoOffset(AreaOffset);
            DamageOnTouch.TargetLayerMask = CharacterHandleWeapon.UseTargetLayerMask
                ? CharacterHandleWeapon.TargetLayerMask
                : TargetLayerMask;

            DamageOnTouch.MinDamageCaused = MinDamageCaused;
            DamageOnTouch.MaxDamageCaused = MaxDamageCaused;
            DamageOnTouch.DamageDirectionMode = EnigmaDamageOnTouch.DamageDirections.BasedOnOwnerPosition;
            DamageOnTouch.DamageCausedKnockbackType = Knockback;
            DamageOnTouch.DamageCausedKnockbackForce = KnockbackForce;
            DamageOnTouch.DamageCausedKnockbackDirection = KnockbackDirection;
            DamageOnTouch.InvincibilityDuration = InvincibilityDuration;
            DamageOnTouch.HitDamageableFeedback = HitDamageableFeedback;
            DamageOnTouch.HitNonDamageableFeedback = HitNonDamageableFeedback;
            DamageOnTouch.TriggerFilter = TriggerFilter;

            if (!CanDamageOwner && (Owner != null))
            {
                DamageOnTouch.IgnoreGameObject(Owner.gameObject);
            }
        }

        public override void WeaponUse()
        {
            EnigmaLogger.Log("MeleeWeapon - WeaponUse");
            base.WeaponUse();
            _attackCoroutine = StartCoroutine(MeleeWeaponAttack());
        }

        protected virtual IEnumerator MeleeWeaponAttack()
        {
            if (_attackInProgress) { yield break; }

            _attackInProgress = true;
            yield return new WaitForSeconds(InitialDelay);
            EnigmaLogger.Log("Enabling Damage Area");
            EnableDamageArea();
            yield return new WaitForSeconds(ActiveDuration);
            DisableDamageArea();
            _attackInProgress = false;
        }

        public override void Interrupt()
        {
            base.Interrupt();
            if (_attackCoroutine != null) { StopCoroutine(_attackCoroutine); }
            DisableDamageArea();
            _attackInProgress = false;
        }
        
        protected virtual void EnableDamageArea()
        {
            if (_damageAreaCollider2D != null) { _damageAreaCollider2D.enabled = true; }
            if (_damageAreaCollider != null)  { _damageAreaCollider.enabled = true;  }
        }

        protected virtual void DisableDamageArea()
        {
            if (_damageAreaCollider2D != null) { _damageAreaCollider2D.enabled = false; }
            if (_damageAreaCollider != null)  { _damageAreaCollider.enabled = false;  }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) { DrawGizmos(); }
        }

        protected virtual void DrawGizmos()
        {
            if (MeleeDamageAreaMode == MeleeDamageAreaModes.Existing) { return; }

            if (DamageAreaShape == MeleeDamageAreaShapes.Box)
            { Gizmos.DrawWireCube(transform.position + AreaOffset, AreaSize); }

            if (DamageAreaShape == MeleeDamageAreaShapes.Circle)
            { Gizmos.DrawWireSphere(transform.position + AreaOffset, AreaSize.x * 0.5f); }

            if (DamageAreaShape == MeleeDamageAreaShapes.Rectangle)
            { MMDebug.DrawGizmoRectangle(transform.position + AreaOffset, AreaSize, Color.red); }

            if (DamageAreaShape == MeleeDamageAreaShapes.Sphere)
            { Gizmos.DrawWireSphere(transform.position + AreaOffset, AreaSize.x * 0.5f); }
        }

        protected virtual void OnDisable() { _attackInProgress = false; }
    }
}
