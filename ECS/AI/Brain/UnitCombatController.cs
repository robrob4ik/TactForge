// File: OneBitRob/AI/UnitCombatController.cs
using System.Collections.Generic;
using MoreMountains.Tools;
using OneBitRob.ECS;
using OneBitRob.EnigmaEngine;
using OneBitRob.FX;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [TemporaryBakingType]
    [RequireComponent(typeof(EnigmaCharacter))]
    public sealed class UnitCombatController : MonoBehaviour
    {
        private EnigmaCharacter _character;
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
        // NEW: cached trigger parameter names (for O(1) lookup)
        private HashSet<string> _animTriggerNames;

        private void Awake()
        {
            _character = GetComponent<EnigmaCharacter>();
            _anim = GetComponentInChildren<Animator>();
            _brain = GetComponent<UnitBrain>();

            var weapon = _brain != null ? _brain.UnitDefinition?.weapon : null;
            if (weapon is RangedWeaponDefinition rw) _projectileId = rw.projectileId;

            // NEW: cache animator trigger names once
            if (_anim != null)
            {
                _animTriggerNames = new HashSet<string>();
                for (int i = 0; i < _anim.parameterCount; i++)
                {
                    var p = _anim.parameters[i];
                    if (p.type == AnimatorControllerParameterType.Trigger)
                        _animTriggerNames.Add(p.name);
                }
            }
            else
            {
                _animTriggerNames = new HashSet<string>();
            }
        }

        public bool IsAlive => _character != null && _character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;

        public void PlayMeleeAttack(AttackAnimationSettings settings)
        {
            if (_anim == null || settings == null || !settings.HasEntries) return;
            var param = settings.SelectParameter(ref _nextMeleeIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        public void PlayRangedPrepare(TwoStageAttackAnimationSettings settings)
        {
            if (_anim == null || settings == null || !settings.HasPrepare) return;
            var param = settings.SelectPrepare(ref _nextPrepareIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        public void PlayRangedFire(TwoStageAttackAnimationSettings settings)
        {
            if (_anim == null || settings == null || !settings.HasFire) return;
            var param = settings.SelectFire(ref _nextFireIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        public void PlaySpell(AttackAnimationSettings settings)
        {
            if (_anim == null || settings == null || !settings.HasEntries) return;
            var param = settings.SelectParameter(ref _nextSpellIdx);
            if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        }

        private bool AnimatorHasTrigger(string param)
        {
            if (_anim == null || string.IsNullOrEmpty(param)) return false;

            if (_animTriggerNames != null && _animTriggerNames.Contains(param))
                return true;

#if UNITY_EDITOR
            if (_missingParams.Add(param))
                Debug.LogWarning($"[{name}] Animator missing Trigger parameter '{param}'. Check your AttackAnimationSet.");
#endif
            return false;
        }

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
                Debug.LogWarning($"[{name}] No projectileId available yet (UnitDefinition/weapon not ready?). Will retry next call.");
                return null;
            }

            _projectilePooler = OneBitRob.VFX.ProjectileService.GetPooler(_projectileId);
            if (_projectilePooler == null) Debug.LogWarning($"[{name}] No projectile pooler found for id '{_projectileId}'. Add your ProjectilePools prefab to this scene or register the id.");

            return _projectilePooler;
        }

        public void FireProjectile(
            Vector3 origin, Vector3 direction, GameObject attacker,
            float speed, float damage, float maxDistance,
            int layerMask,
            float critChance = 0f, float critMultiplier = 1f,
            float pierceChance = 0f, int pierceMaxTargets = 0)
        {
            var pooler = ResolveProjectilePooler();
            if (pooler == null) return;

            GameObject go = pooler.GetPooledGameObject();
            if (go == null) return;

            var poolable = go.GetComponent<MMPoolableObject>();
            var proj = go.GetComponent<WeaponProjectile>();

            go.transform.position = origin;
            go.transform.forward = (direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized);

            proj.Arm(
                new WeaponProjectile.ArmData
                {
                    Attacker = attacker,
                    Origin = origin,
                    Direction = direction,
                    Speed = (speed > 0f ? speed : 60f),
                    Damage = damage,
                    MaxDistance = (maxDistance > 0f ? maxDistance : 40f),
                    LayerMask = (layerMask != 0 ? layerMask : (_brain != null ? _brain.GetDamageableLayerMask().value : ~0)),
                    CritChance = Mathf.Clamp01(critChance),
                    CritMultiplier = Mathf.Max(1f, critMultiplier),
                    PierceChance = Mathf.Clamp01(pierceChance),
                    PierceMaxTargets = Mathf.Max(0, pierceMaxTargets)
                }
            );

            go.SetActive(true);
            poolable?.TriggerOnSpawnComplete();
        }

        public void FireSpellProjectile(
            string projectileId,
            Vector3 origin,
            Vector3 direction,
            GameObject attacker,
            float speed,
            float damage,
            float maxDistance,
            int layerMask,
            float radius,
            bool pierce,
            FeedbackDefinition hitFeedback = null
        )
        {
            if (string.IsNullOrEmpty(projectileId)) return;
            var pooler = OneBitRob.VFX.ProjectileService.GetPooler(projectileId);
            if (pooler == null)
            {
                Debug.LogWarning($"[{name}] Spell projectile pool '{projectileId}' not found in this scene.");
                return;
            }

            GameObject go = pooler.GetPooledGameObject();
            if (go == null) return;

            var poolable = go.GetComponent<MMPoolableObject>();
            var proj = go.GetComponent<SpellProjectile>();

            if (proj == null)
            {
                Debug.LogError($"[{name}] Spell projectile must have SpellProjectile + MMPoolableObject.");
                return;
            }

            go.transform.position = origin;
            go.transform.forward = (direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized);

            proj.Arm(
                new SpellProjectile.ArmData
                {
                    Attacker = attacker,
                    Origin = origin,
                    Direction = direction,
                    Speed = speed > 0f ? speed : 60f,
                    Damage = damage,
                    MaxDistance = maxDistance > 0f ? maxDistance : 20f,
                    LayerMask = layerMask != 0 ? layerMask : (GetComponent<UnitBrain>()?.GetDamageableLayerMask().value ?? ~0),
                    Radius = Mathf.Max(0f, radius),
                    Pierce = pierce,
                    HitFeedback = hitFeedback // unchanged
                }
            );

            go.SetActive(true);
            poolable?.TriggerOnSpawnComplete();
        }
    }
}
