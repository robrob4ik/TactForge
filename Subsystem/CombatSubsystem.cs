
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
    
    private void Awake()
    {
        _character      = GetComponent<EnigmaCharacter>();
        _characterHandleWeapon   = _character.FindAbility<EnigmaCharacterHandleWeapon>();
        _orientation3D  = _character.FindAbility<EnigmaCharacterOrientation3D>();
    }
    
    public bool IsAlive =>
        _character != null &&
        _character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;

    public void Attack()
    {
        _characterHandleWeapon.ShootStart();
    }

    public void StopAttack() => _characterHandleWeapon.ShootStop();

    public void AimAtTarget(Transform target)
    {
        if (_characterHandleWeapon.CurrentWeapon != null)
        {
            if (_enigmaWeaponAim == null)
            {
                _enigmaWeaponAim = _characterHandleWeapon.CurrentWeapon.gameObject.MMGetComponentNoAlloc<EnigmaWeaponAim>();
            }                 
        }
        EnigmaLogger.Log("AimAtTarget:"+target.transform.position);
        _enigmaWeaponAim.SetCurrentAim(target.transform.position - _character.transform.position);
    }
}

