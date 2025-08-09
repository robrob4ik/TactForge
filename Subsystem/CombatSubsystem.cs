using MoreMountains.Tools;
using OneBitRob.EnigmaEngine;
using Unity.Entities;
using UnityEngine;

[TemporaryBakingType]
[RequireComponent(typeof(EnigmaCharacter))]
public class CombatSubsystem : MonoBehaviour
{
    private EnigmaCharacter               _character;
    private EnigmaCharacterHandleWeapon   _characterHandleWeapon;
    private EnigmaCharacterOrientation3D  _orientation3D; 
    
    private EnigmaWeaponAim _enigmaWeaponAim;
    private bool _warnedMissingAim;

    private void Awake()
    {
        _character      = GetComponent<EnigmaCharacter>();
        _characterHandleWeapon   = _character.FindAbility<EnigmaCharacterHandleWeapon>();
        _orientation3D  = _character.FindAbility<EnigmaCharacterOrientation3D>();
    }
    
    public bool IsAlive => _character != null && _character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;

    public void Attack()
    {
        _characterHandleWeapon.ShootStart();
    }

    public void StopAttack() => _characterHandleWeapon.ShootStop();

    public void AimAtTarget(Transform target)
    {
        if (_characterHandleWeapon.CurrentWeapon != null && _enigmaWeaponAim == null)
            _enigmaWeaponAim = _characterHandleWeapon.CurrentWeapon.gameObject.MMGetComponentNoAlloc<EnigmaWeaponAim>();

        if (_enigmaWeaponAim == null)
        {
#if UNITY_EDITOR
            if (!_warnedMissingAim)
            {
                Debug.LogWarning($"[{name}] EnigmaWeaponAim missing on current weapon; cannot aim.");
                _warnedMissingAim = true;
            }
#endif
            return;
        }

        var dir = target.transform.position - _character.transform.position;
        _enigmaWeaponAim.SetCurrentAim(dir);
    }
}