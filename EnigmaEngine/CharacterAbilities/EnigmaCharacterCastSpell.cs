using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using OneBitRob.AI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace OneBitRob.EnigmaEngine
{
    public class EnigmaCharacterCastSpell : EnigmaCharacterAbility
    {
        private UnitBrain _brain;

        public SpellDefinition CurrentSpell;
        // TODO Unused?
        public bool AbilityCasting = false;
        protected float _lastCastTime = -Mathf.Infinity;
        
        protected const string _isCastingAnimationParameterName = "Casting";
        protected int _isCastingAnimationParameter;

        protected override void Initialization()
        {
            _brain = GetComponentInParent<UnitBrain>();
            RegisterAnimatorParameter(_isCastingAnimationParameterName, AnimatorControllerParameterType.Bool, out _isCastingAnimationParameter);
        }

        public bool ReadyToCast()
        {
            if (CurrentSpell == null) return false;
            if (Time.time < _lastCastTime + CurrentSpell.Cooldown) return false;

            return true;
        }

        public bool CanCast()
        {
            switch (CurrentSpell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    if (_brain.CurrentSpellTarget == null) return false;
                    float dist = Vector3.Distance(transform.position, _brain.CurrentSpellTarget.transform.position);
                    if (dist > CurrentSpell.Range) return false;
                    break;

                case SpellTargetType.MultiTarget:
                    if (_brain.CurrentSpellTargets == null || _brain.CurrentSpellTargets.Count == 0) return false;
                    break;

                case SpellTargetType.AreaOfEffect:
                    if (!_brain.CurrentSpellTargetPosition.HasValue) return false;
                    break;
            }

            return true;
        }

        public bool TryCastSpell(GameObject target = null, List<GameObject> targets = null, Vector3? aoePosition = null)
        {
            StartCoroutine(CastRoutine(target, targets, aoePosition));
            return true;
        }

        protected IEnumerator CastRoutine(GameObject target, List<GameObject> targets, Vector3? aoePosition)
        {
            _lastCastTime = Time.time;

            PlayAbilityStartFeedbacks();
            PlayAbilityStartSfx();
            Debug.Log($"[SpellCaster] Casting spell: {CurrentSpell.SpellName}");

            AbilityCasting = true;
            Debug.Log($"[SpellCaster] ABILITY CASTING SET TO TRUE");
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _isCastingAnimationParameter, true, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            yield return new WaitForSeconds(CurrentSpell.CastTime);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _isCastingAnimationParameter, false, _character._animatorParameters, _character.RunAnimatorSanityChecks);
            Debug.Log($"[SpellCaster] ABILITY CASTING SET TO FALSEEEEEEE");
            AbilityCasting = false;

            switch (CurrentSpell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    if (target != null) StartCoroutine(ApplyEffectAsync(target));
                    break;

                case SpellTargetType.MultiTarget:
                    if (targets != null)
                    {
                        int count = Mathf.Min(targets.Count, CurrentSpell.MaxTargets);
                        for (int i = 0; i < count; i++)
                        {
                            StartCoroutine(ApplyEffectAsync(targets[i]));
                            yield return new WaitForSeconds(CurrentSpell.ChainJumpDelay);
                        }
                    }

                    break;

                case SpellTargetType.AreaOfEffect:
                    if (aoePosition.HasValue)
                    {
                        Vector3 center = aoePosition.Value;
                        Collider[] colliders = Physics.OverlapSphere(center, CurrentSpell.AreaRadius, CurrentSpell.TargetLayerMask);

                        foreach (var col in colliders) { StartCoroutine(ApplyEffectAsync(col.gameObject)); }

                        if (CurrentSpell.SpellEffectPrefab != null)
                        {
                            var handle = CurrentSpell.SpellEffectPrefab.InstantiateAsync(center, Quaternion.identity);
                            yield return handle;
                            Addressables.ReleaseInstance(handle.Result);
                        }
                    }

                    break;
            }

            PlayAbilityStopFeedbacks();
            PlayAbilityStopSfx();
        }

        protected IEnumerator ApplyEffectAsync(GameObject target)
        {
            Debug.Log($"[SpellCaster] Applying {CurrentSpell.SpellName} to {target.name}");

            if (CurrentSpell.SpellEffectPrefab == null) yield break;

            AsyncOperationHandle<GameObject> handle = CurrentSpell.SpellEffectPrefab.LoadAssetAsync<GameObject>();
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[SpellCaster] Failed to load spell prefab for {CurrentSpell.SpellName}");
                yield break;
            }

            GameObject instance = Instantiate(handle.Result);
            Debug.Log($"[SpellCaster] InstantiaING spell prefab for {CurrentSpell.SpellName}");
            switch (CurrentSpell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    var single = instance.GetComponent<SingleTargetEffectOverTimeSpell>();
                    if (single != null)
                    {
                        Debug.Log($"[SpellCaster] InstantiaED spell prefab for {CurrentSpell.SpellName}");
                        single.Initialize(CurrentSpell, target);
                    }

                    break;

                case SpellTargetType.MultiTarget:
                    var chain = instance.GetComponent<ChainedSpell>();
                    if (chain != null) chain.Initialize(CurrentSpell, target);
                    break;

                case SpellTargetType.AreaOfEffect:
                    var aoe = instance.GetComponent<AreaOfEffectSpell>();
                    if (aoe != null) aoe.Initialize(CurrentSpell, target.transform.position);
                    break;
            }

            instance.SetActive(true);
            Debug.Log($"[SpellCaster] ACTIVATED spell prefab for {CurrentSpell.SpellName}");
        }
    }
}