using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using OneBitRob.Constants;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Weapons/Enigma Melee Weapon")]
    public class EnigmaMeleeWeapon : EnigmaWeapon
    {
        public enum MeleeDamageAreaShapes
        {
            Rectangle,
            Circle,
            Box,
            Sphere
        }

        public enum MeleeDamageAreaModes
        {
            Generated,
            Existing
        }

        [FoldoutGroup("Damage Area"), Title("Damage Area")]
        [Tooltip("The possible modes to handle the damage area. In Generated, the MeleeWeapon will create it, in Existing, you can bind an existing damage area - usually nested under the weapon")]
        public MeleeDamageAreaModes MeleeDamageAreaMode = MeleeDamageAreaModes.Generated;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The shape of the damage area (rectangle or circle)")]
        public MeleeDamageAreaShapes DamageAreaShape = MeleeDamageAreaShapes.Rectangle;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The offset to apply to the damage area (from the weapon's attachment position)")]
        public Vector3 AreaOffset = new Vector3(1, 0);

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The size of the damage area")]
        public Vector3 AreaSize = new Vector3(1, 1);

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The trigger filters this melee weapon should apply damage on")]
        public EnigmaDamageOnTouch.TriggerAndCollisionMask TriggerFilter = EnigmaDamageOnTouch.AllowedTriggerCallbacks;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The feedback to play when hitting a Damageable")]
        public MMFeedbacks HitDamageableFeedback;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Generated")]
        [Tooltip("The feedback to play when hitting a non Damageable")]
        public MMFeedbacks HitNonDamageableFeedback;

        [FoldoutGroup("Damage Area")]
        [ShowIf("@MeleeDamageAreaMode == MeleeDamageAreaModes.Existing")]
        [Tooltip("An existing damage area to activate/handle as the weapon is used")]
        public EnigmaDamageOnTouch ExistingDamageArea;

        [FoldoutGroup("Damage Area Timing"), Title("Damage Area Timing")]
        [Tooltip("The initial delay to apply before triggering the damage area")]
        public float InitialDelay = 0f;

        [FoldoutGroup("Damage Area Timing")]
        [Tooltip("The duration during which the damage area is active")]
        public float ActiveDuration = 1f;

        [Title("Damage Caused")]
        [FoldoutGroup("Damage Caused")]
        [Tooltip("The min amount of health to remove from the player's health")]
        public float MinDamageCaused = 10f;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("The max amount of health to remove from the player's health")]
        public float MaxDamageCaused = 10f;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("The kind of knockback to apply")]
        public EnigmaDamageOnTouch.KnockbackStyles Knockback;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("The force to apply to the object that gets damaged")]
        public Vector3 KnockbackForce = new Vector3(10, 2, 0);

        [FoldoutGroup("Damage Caused")]
        [Tooltip("The direction in which to apply the knockback")]
        public EnigmaDamageOnTouch.KnockbackDirections KnockbackDirection = EnigmaDamageOnTouch.KnockbackDirections.BasedOnOwnerPosition;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("The duration of the invincibility frames after the hit (in seconds)")]
        public float InvincibilityDuration = 0.5f;

        [FoldoutGroup("Damage Caused")]
        [Tooltip("If this is true, the owner can be damaged by its own weapon's damage area (usually false)")]
        public bool CanDamageOwner = false;

        protected Collider _damageAreaCollider;
        protected Collider2D _damageAreaCollider2D;
        protected bool _attackInProgress = false;
        protected Color _gizmosColor;
        protected Vector3 _gizmoSize;
        protected CircleCollider2D _circleCollider2D;
        protected BoxCollider2D _boxCollider2D;
        protected BoxCollider _boxCollider;
        protected SphereCollider _sphereCollider;
        protected Vector3 _gizmoOffset;
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

            if (Owner != null) { DamageOnTouch.Owner = Owner.gameObject; }
        }

        protected virtual void CreateDamageArea()
        {
            if ((MeleeDamageAreaMode == MeleeDamageAreaModes.Existing) && (ExistingDamageArea != null))
            {
                _damageArea = ExistingDamageArea.gameObject;
                _damageAreaCollider = _damageArea.gameObject.GetComponent<Collider>();
                _damageAreaCollider2D = _damageArea.gameObject.GetComponent<Collider2D>();
                DamageOnTouch = ExistingDamageArea;
                return;
            }

            _damageArea = new GameObject();
            _damageArea.name = this.name + "DamageArea";
            _damageArea.transform.position = this.transform.position;
            _damageArea.transform.rotation = this.transform.rotation;
            _damageArea.transform.SetParent(this.transform);
            _damageArea.transform.localScale = Vector3.one;
            _damageArea.layer = CharacterHandleWeapon.DamageableLayer;

            if (DamageAreaShape == MeleeDamageAreaShapes.Rectangle)
            {
                _boxCollider2D = _damageArea.AddComponent<BoxCollider2D>();
                _boxCollider2D.offset = AreaOffset;
                _boxCollider2D.size = AreaSize;
                _damageAreaCollider2D = _boxCollider2D;
                _damageAreaCollider2D.isTrigger = true;
            }

            if (DamageAreaShape == MeleeDamageAreaShapes.Circle)
            {
                _circleCollider2D = _damageArea.AddComponent<CircleCollider2D>();
                _circleCollider2D.transform.position = this.transform.position;
                _circleCollider2D.offset = AreaOffset;
                _circleCollider2D.radius = AreaSize.x / 2;
                _damageAreaCollider2D = _circleCollider2D;
                _damageAreaCollider2D.isTrigger = true;
            }

            if ((DamageAreaShape == MeleeDamageAreaShapes.Rectangle) || (DamageAreaShape == MeleeDamageAreaShapes.Circle))
            {
                Rigidbody2D rigidBody = _damageArea.AddComponent<Rigidbody2D>();
                rigidBody.isKinematic = true;
                rigidBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
            }

            if (DamageAreaShape == MeleeDamageAreaShapes.Box)
            {
                _boxCollider = _damageArea.AddComponent<BoxCollider>();
                _boxCollider.center = AreaOffset;
                _boxCollider.size = AreaSize;
                _damageAreaCollider = _boxCollider;
                _damageAreaCollider.isTrigger = true;
            }

            if (DamageAreaShape == MeleeDamageAreaShapes.Sphere)
            {
                _sphereCollider = _damageArea.AddComponent<SphereCollider>();
                _sphereCollider.transform.position = this.transform.position + this.transform.rotation * AreaOffset;
                _sphereCollider.radius = AreaSize.x / 2;
                _damageAreaCollider = _sphereCollider;
                _damageAreaCollider.isTrigger = true;
            }

            if ((DamageAreaShape == MeleeDamageAreaShapes.Box) || (DamageAreaShape == MeleeDamageAreaShapes.Sphere))
            {
                Rigidbody rigidBody = _damageArea.AddComponent<Rigidbody>();
                rigidBody.isKinematic = true;

                rigidBody.gameObject.AddComponent<MMRagdollerIgnore>();
            }

            DamageOnTouch = _damageArea.AddComponent<EnigmaDamageOnTouch>();
            DamageOnTouch.SetGizmoSize(AreaSize);
            DamageOnTouch.SetGizmoOffset(AreaOffset);
            DamageOnTouch.TargetLayerMask = CharacterHandleWeapon.UseTargetLayerMask ? CharacterHandleWeapon.TargetLayerMask : TargetLayerMask;
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

            if (!CanDamageOwner && (Owner != null)) { DamageOnTouch.IgnoreGameObject(Owner.gameObject); }
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
        }

        protected virtual void EnableDamageArea()
        {
            if (_damageAreaCollider2D != null) { _damageAreaCollider2D.enabled = true; }

            if (_damageAreaCollider != null) { _damageAreaCollider.enabled = true; }
        }

        protected virtual void DisableDamageArea()
        {
            if (_damageAreaCollider2D != null) { _damageAreaCollider2D.enabled = false; }

            if (_damageAreaCollider != null) { _damageAreaCollider.enabled = false; }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) { DrawGizmos(); }
        }

        protected virtual void DrawGizmos()
        {
            if (MeleeDamageAreaMode == MeleeDamageAreaModes.Existing) { return; }

            if (DamageAreaShape == MeleeDamageAreaShapes.Box) { Gizmos.DrawWireCube(this.transform.position + AreaOffset, AreaSize); }

            if (DamageAreaShape == MeleeDamageAreaShapes.Circle) { Gizmos.DrawWireSphere(this.transform.position + AreaOffset, AreaSize.x / 2); }

            if (DamageAreaShape == MeleeDamageAreaShapes.Rectangle) { MMDebug.DrawGizmoRectangle(this.transform.position + AreaOffset, AreaSize, Color.red); }

            if (DamageAreaShape == MeleeDamageAreaShapes.Sphere) { Gizmos.DrawWireSphere(this.transform.position + AreaOffset, AreaSize.x / 2); }
        }

        protected virtual void OnDisable() { _attackInProgress = false; }
    }
}