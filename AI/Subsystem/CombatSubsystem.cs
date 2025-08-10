// FILE: CombatSubsystem.cs
// Changes applied:
// - Removed any WeaponAim logic (already done earlier).
// - Added projectile spawning API (FireProjectile) with a shared MMObjectPooler reference.

using MoreMountains.Tools;
using OneBitRob.AI;
using OneBitRob.EnigmaEngine;
using OneBitRob.ECS;
using Unity.Entities;
using UnityEngine;

[TemporaryBakingType]
[RequireComponent(typeof(EnigmaCharacter))]
public class CombatSubsystem : MonoBehaviour
{
    private EnigmaCharacter _character;
    private EnigmaCharacterHandleWeapon _characterHandleWeapon;
    private EnigmaCharacterOrientation3D _orientation3D;
    private Animator _anim;

    private int _nextMeleeIdx;
    private int _nextPrepareIdx;
    private int _nextFireIdx;

    [Header("Fallback Animations (optional)")]
    [SerializeField] private OneBitRob.AttackAnimationSet _fallbackMeleeAnimations;
    [SerializeField] private OneBitRob.TwoStageAttackAnimationSet _fallbackRangedAnimations;

    [Header("Projectile Spawning (shared pooler recommended)")]
    [Tooltip("Scene-level pooler; do NOT put one per unit. Assign a shared pooler.")]
    public MMObjectPooler ProjectilePooler;

    [Tooltip("If true, use the TopDown handle weapon's mask when available.")]
    public bool UseHandleWeaponLayerMask = false;

    [Tooltip("If non-zero, overrides all other masks.")]
    public LayerMask TargetMaskOverride;

    private UnitBrain _brain;

    private void Awake()
    {
        _character = GetComponent<EnigmaCharacter>();
        _characterHandleWeapon = _character.FindAbility<EnigmaCharacterHandleWeapon>();
        _orientation3D = _character.FindAbility<EnigmaCharacterOrientation3D>();
        _anim = GetComponentInChildren<Animator>();
        _brain = GetComponent<UnitBrain>();
    }

    public bool IsAlive => _character != null && _character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;

    // Legacy compatibility (kept)
    public void Attack() => _characterHandleWeapon.ShootStart();
    public void StopAttack() => _characterHandleWeapon.ShootStop();

    public void PlayMeleeAttack(OneBitRob.AttackAnimationSet set)
    {
        if (_anim == null) return;
        var use = (set != null && set.HasEntries) ? set : _fallbackMeleeAnimations;
        if (use == null || !use.HasEntries) return;

        var param = use.SelectParameter(ref _nextMeleeIdx);
        if (!string.IsNullOrEmpty(param))
            _anim.SetTrigger(param);
    }

    public void PlayRangedPrepare(OneBitRob.TwoStageAttackAnimationSet set)
    {
        if (_anim == null || set == null) return;
        var use = set.HasPrepare ? set : _fallbackRangedAnimations;
        if (use == null || !use.HasPrepare) return;

        var param = use.SelectPrepare(ref _nextPrepareIdx);
        if (!string.IsNullOrEmpty(param))
            _anim.SetTrigger(param);
    }

    public void PlayRangedFire(OneBitRob.TwoStageAttackAnimationSet set)
    {
        if (_anim == null || set == null) return;
        var use = set.HasFire ? set : _fallbackRangedAnimations;
        if (use == null || !use.HasFire) return;

        var param = use.SelectFire(ref _nextFireIdx);
        if (!string.IsNullOrEmpty(param))
            _anim.SetTrigger(param);
    }

    /// <summary>
    /// Spawns a pooled projectile and arms it with the proper data.
    /// Use a shared (scene-level) MMObjectPooler; do NOT have one per unit.
    /// </summary>
    public void FireProjectile(Vector3 origin, Vector3 direction, GameObject attacker, float speed, float damage, float maxDistance)
    {
        if (ProjectilePooler == null) return;

        var go = ProjectilePooler.GetPooledGameObject();
        if (go == null) return;

        var poolable = go.GetComponent<MMPoolableObject>();
        var proj = go.GetComponent<EcsProjectile>();
#if UNITY_EDITOR
        if (proj == null)
        {
            Debug.LogError($"[{name}] Pooled projectile must have EcsProjectile + MMPoolableObject.");
            return;
        }
#endif
        go.transform.position = origin;
        go.transform.forward  = direction;

        int layerMask = (TargetMaskOverride.value != 0)
            ? TargetMaskOverride.value
            : (UseHandleWeaponLayerMask && _characterHandleWeapon != null && _characterHandleWeapon.UseTargetLayerMask
                ? _characterHandleWeapon.TargetLayerMask.value
                : (_brain != null ? _brain.GetTargetLayerMask().value : ~0));

        proj.Arm(new EcsProjectile.ArmData
        {
            Attacker    = attacker,
            Origin      = origin,
            Direction   = direction,
            Speed       = (speed > 0f ? speed : 60f),
            Damage      = (damage > 0f ? damage : 10f),
            MaxDistance = (maxDistance > 0f ? maxDistance : 40f),
            LayerMask   = layerMask
        });

        go.SetActive(true);
        if (poolable != null) poolable.TriggerOnSpawnComplete();
    }
}
