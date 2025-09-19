// File: Assets/PROJECT/Scripts/Mono/Combat/UnitCombatController.cs
using System.Collections.Generic;
using MoreMountains.Tools;
using OneBitRob.Anim;
using UnityEngine;
using OneBitRob.Config;
using OneBitRob.ECS;
using OneBitRob.EnigmaEngine;
using OneBitRob.FX;
using OneBitRob.VFX;
using Unity.Entities;

namespace OneBitRob.AI
{
    [TemporaryBakingType]
    [RequireComponent(typeof(EnigmaCharacter))]
    public sealed class UnitCombatController : MonoBehaviour
    {
        private EnigmaCharacter _character;
        private Animator _anim;
        private UnitBrain _brain;
        private UnitAnimator _unitAnim;

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
            _anim = GetComponentInChildren<Animator>();
            _brain = GetComponent<UnitBrain>();
            _unitAnim = GetComponent<UnitAnimator>();

            var weapon = _brain != null ? _brain.UnitDefinition?.weapon : null;
            if (weapon is RangedWeaponDefinition rw) _projectileId = rw.projectileId;
        }

        public bool IsAlive => _character != null && _character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;

        // public void PlayMeleeAttack(AttackAnimationSettings settings)
        // {
        //     // Compute Animator first
        //     var meleeCompute = (_brain?.UnitDefinition?.weapon as MeleeWeaponDefinition)?.attackAnimationsCompute
        //                        ?? _unitAnim?.AnimationsDefinition?.Melee;
        //     if (_unitAnim && _unitAnim.IsComputeActive && meleeCompute && meleeCompute.HasEntries)
        //     {
        //         _unitAnim.PlayMelee(meleeCompute);
        //         return;
        //     }
        //
        //     // fallback (existing)
        //     if (_anim == null || settings == null || !settings.HasEntries) return;
        //     var param = settings.SelectParameter(ref _nextMeleeIdx);
        //     if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        // }
        //
        // public void PlayRangedPrepare(TwoStageAttackAnimationSettings settings)
        // {
        //     var rangedCompute = (_brain?.UnitDefinition?.weapon as RangedWeaponDefinition)?.animationsCompute
        //                         ?? _unitAnim?.AnimationsDefinition?.Ranged;
        //     if (_unitAnim && _unitAnim.IsComputeActive && rangedCompute && rangedCompute.HasPrepare)
        //     {
        //         _unitAnim.PlayRangedPrepare(rangedCompute);
        //         return;
        //     }
        //
        //     if (_anim == null || settings == null || !settings.HasPrepare) return;
        //     var param = settings.SelectPrepare(ref _nextPrepareIdx);
        //     if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        // }
        //
        // public void PlayRangedFire(TwoStageAttackAnimationSettings settings)
        // {
        //     var rangedCompute = (_brain?.UnitDefinition?.weapon as RangedWeaponDefinition)?.animationsCompute
        //                         ?? _unitAnim?.AnimationsDefinition?.Ranged;
        //     if (_unitAnim && _unitAnim.IsComputeActive && rangedCompute && rangedCompute.HasFire)
        //     {
        //         _unitAnim.PlayRangedFire(rangedCompute);
        //         return;
        //     }
        //
        //     if (_anim == null || settings == null || !settings.HasFire) return;
        //     var param = settings.SelectFire(ref _nextFireIdx);
        //     if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        // }
        //
        // public void PlaySpell(AttackAnimationSettings settings)
        // {
        //     var spells = _brain?.UnitDefinition?.unitSpells;
        //     var compute = (spells != null && spells.Count > 0 && spells[0] != null)
        //         ? spells[0].castAnimations
        //         : _unitAnim?.animsDefinition?.DefaultSpell;
        //
        //     if (_unitAnim && _unitAnim.IsComputeActive && compute && compute.HasEntries)
        //     {
        //         _unitAnim.PlaySpell(compute);
        //         return;
        //     }
        //
        //     if (_anim == null || settings == null || !settings.HasEntries) return;
        //     var param = settings.SelectParameter(ref _nextSpellIdx);
        //     if (!string.IsNullOrEmpty(param) && AnimatorHasTrigger(param)) _anim.SetTrigger(param);
        // }

        public void PlayMeleeAttackCompute()
        {
            if (!_unitAnim) return;
            var set = (_brain?.UnitDefinition?.weapon as MeleeWeaponDefinition)?.attackAnimations;
            _unitAnim.PlayMelee(set);
        }

        public void PlayRangedPrepareCompute()
        {
            if (!_unitAnim) return;
            var set = (_brain?.UnitDefinition?.weapon as RangedWeaponDefinition)?.attackAnimations;
            _unitAnim.PlayRangedPrepare(set);
        }

        public void PlayRangedFireCompute()
        {
            if (!_unitAnim) return;
            var set = (_brain?.UnitDefinition?.weapon as RangedWeaponDefinition)?.attackAnimations;
            _unitAnim.PlayRangedFire(set);
        }

        public void PlaySpellCompute()
        {
            if (!_unitAnim) return;
            ComputeAttackAnimationSettings set = null;
            var spells = _brain?.UnitDefinition?.unitSpells;
            if (spells != null && spells.Count > 0 && spells[0] != null)
                set = spells[0].castAnimations;
            _unitAnim.PlaySpell(set);
        }
        
        private bool AnimatorHasTrigger(string param)
        {
            if (_anim == null) return false;
            for (int i = 0; i < _anim.parameterCount; i++)
            {
                var p = _anim.parameters[i];
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == param) return true;
            }

#if UNITY_EDITOR
            if (_missingParams.Add(param)) Debug.LogWarning($"[{name}] Animator missing Trigger parameter '{param}'. Check your AttackAnimationSet.");
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

            _projectilePooler = ProjectileService.GetPooler(_projectileId);
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
            // Use PoolHub (safe) instead of raw pooler call
            if (string.IsNullOrEmpty(_projectileId))
            {
                // populate id lazily from definition
                ResolveProjectilePooler();
            }

            GameObject go = PoolHub.GetPooled(PoolKind.Projectile, _projectileId);
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

            // SAFE pooled retrieval via PoolHub
            GameObject go = PoolHub.GetPooled(PoolKind.Projectile, projectileId);
            if (go == null)
            {
                Debug.LogWarning($"[{name}] Spell projectile pool '{projectileId}' not found or empty in this scene.");
                return;
            }

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
                    HitFeedback = hitFeedback
                }
            );

            go.SetActive(true);
            poolable?.TriggerOnSpawnComplete();
        }
    }
}