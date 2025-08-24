// Runtime/AI/Combat/CombatSubsystem.cs
using System.Collections.Generic;
using MoreMountains.Tools;
using OneBitRob.EnigmaEngine;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [TemporaryBakingType]
    [RequireComponent(typeof(EnigmaCharacter))]
    public sealed class CombatSubsystem : MonoBehaviour
    {
        private EnigmaCharacter _character;
        private EnigmaCharacterHandleWeapon _characterHandleWeapon;
        private Animator _anim;
        private UnitBrain _brain;

        private int _nextMeleeIdx;
        private int _nextPrepareIdx;
        private int _nextFireIdx;
        private int _nextSpellIdx;

        private MMObjectPooler _projectilePooler;
        private string _projectileId;

#if UNITY_EDITOR
        private readonly HashSet<string> _missingParams = new();
#endif

        private void Awake()
        {
            _character = GetComponent<EnigmaCharacter>();
            _characterHandleWeapon = _character.FindAbility<EnigmaCharacterHandleWeapon>();
            _anim = GetComponentInChildren<Animator>();
            _brain = GetComponent<UnitBrain>();

            var weapon = _brain != null ? _brain.UnitDefinition?.weapon : null;
            if (weapon is RangedWeaponDefinition rw) _projectileId = rw.projectileId;
        }

        public bool IsAlive =>
            _character != null &&
            _character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;

        public void PlayMeleeAttack(OneBitRob.AttackAnimationSet set)
        {
            if (_anim == null || set == null || !set.HasEntries) return;
            var param = set.SelectParameter(ref _nextMeleeIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        public void PlayRangedPrepare(TwoStageAttackAnimationSet set)
        {
            if (_anim == null || set == null || !set.HasPrepare) return;
            var param = set.SelectPrepare(ref _nextPrepareIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        public void PlayRangedFire(TwoStageAttackAnimationSet set)
        {
            if (_anim == null || set == null || !set.HasFire) return;
            var param = set.SelectFire(ref _nextFireIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        public void PlaySpell(OneBitRob.AttackAnimationSet set)
        {
            if (_anim == null || set == null || !set.HasEntries) return;
            var param = set.SelectParameter(ref _nextSpellIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        public void PlaySpellPrepare(TwoStageAttackAnimationSet set) { }
        public void PlaySpellFire   (TwoStageAttackAnimationSet set) { }

#if UNITY_EDITOR
        private bool AnimatorHasTrigger(string param)
        {
            if (_anim == null) return false;
            for (int i = 0; i < _anim.parameterCount; i++)
            {
                var p = _anim.parameters[i];
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == param) return true;
            }
            if (_missingParams.Add(param))
                Debug.LogWarning($"[{name}] Animator missing Trigger parameter '{param}'. Check your AttackAnimationSet.");
            return false;
        }
#else
        private bool AnimatorHasTrigger(string _) => true;
#endif

        public bool HasRangedProjectileConfigured() => ResolveProjectilePooler() != null;

        private MMObjectPooler ResolveProjectilePooler()
        {
            if (_projectilePooler != null) return _projectilePooler;

            if (string.IsNullOrEmpty(_projectileId) && _brain != null)
            {
                var w = _brain.UnitDefinition != null ? _brain.UnitDefinition.weapon : null;
                if (w is RangedWeaponDefinition rw) _projectileId = rw.projectileId;
            }

            if (string.IsNullOrEmpty(_projectileId))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[{name}] No projectileId available yet (UnitDefinition/weapon not ready?). Will retry next call.");
#endif
                return null;
            }

            _projectilePooler = OneBitRob.ECS.ProjectilePoolManager.Resolve(_projectileId);
#if UNITY_EDITOR
            if (_projectilePooler == null)
                Debug.LogWarning($"[{name}] No projectile pooler found for id '{_projectileId}'. Add your ProjectilePools prefab to this scene or register the id.");
#endif
            return _projectilePooler;
        }

        // ─────────────────────────────────────────── Ranged projectile (with crit)
        public void FireProjectile(
            Vector3 origin, Vector3 direction, GameObject attacker,
            float speed, float damage, float maxDistance,
            int layerMask,
            float critChance = 0f, float critMultiplier = 1f)
        {
            var pooler = ResolveProjectilePooler();
            if (pooler == null) return;

            GameObject go = pooler.GetPooledGameObject();
            if (go == null) return;

            var poolable = go.GetComponent<MMPoolableObject>();
            var proj = go.GetComponent<OneBitRob.ECS.WeaponProjectile>();
#if UNITY_EDITOR
            if (proj == null)
            {
                Debug.LogError($"[{name}] Pooled projectile must have WeaponProjectile + MMPoolableObject.");
                return;
            }
#endif
            go.transform.position = origin;
            go.transform.forward = (direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized);

            proj.Arm(
                new OneBitRob.ECS.WeaponProjectile.ArmData
                {
                    Attacker = attacker,
                    Origin = origin,
                    Direction = direction,
                    Speed = (speed > 0f ? speed : 60f),
                    Damage = damage,
                    MaxDistance = (maxDistance > 0f ? maxDistance : 40f),
                    LayerMask = (layerMask != 0 ? layerMask : (_brain != null ? _brain.GetDamageableLayerMask().value : ~0)),
                    CritChance = Mathf.Clamp01(critChance),
                    CritMultiplier = Mathf.Max(1f, critMultiplier)
                }
            );

            go.SetActive(true);
            poolable?.TriggerOnSpawnComplete();
        }

        // ───────── Spell projectile
        public void FireSpellProjectile(string projectileId, Vector3 origin, Vector3 direction, GameObject attacker,
            float speed, float damage, float maxDistance, int layerMask, float radius, bool pierce)
        {
            if (string.IsNullOrEmpty(projectileId)) return;
            var pooler = OneBitRob.ECS.ProjectilePoolManager.Resolve(projectileId);
            if (pooler == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[{name}] Spell projectile pool '{projectileId}' not found in this scene.");
#endif
                return;
            }

            GameObject go = pooler.GetPooledGameObject();
            if (go == null) return;

            var poolable = go.GetComponent<MMPoolableObject>();
            var proj = go.GetComponent<OneBitRob.ECS.SpellProjectile>();
#if UNITY_EDITOR
            if (proj == null)
            {
                Debug.LogError($"[{name}] Spell projectile must have SpellProjectile + MMPoolableObject.");
                return;
            }
#endif
            go.transform.position = origin;
            go.transform.forward = (direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized);

            proj.Arm(
                new ECS.SpellProjectile.ArmData
                {
                    Attacker = attacker,
                    Origin = origin,
                    Direction = direction,
                    Speed = speed > 0f ? speed : 60f,
                    Damage = damage,
                    MaxDistance = maxDistance > 0f ? maxDistance : 20f,
                    LayerMask = layerMask != 0 ? layerMask : (_brain != null ? _brain.GetDamageableLayerMask().value : ~0),
                    Radius = Mathf.Max(0f, radius),
                    Pierce = pierce
                }
            );

            go.SetActive(true);
            poolable?.TriggerOnSpawnComplete();
        }
    }
}
