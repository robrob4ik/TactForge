using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{
    // TODO I guess Unused? To Remove
    [AddComponentMenu("Enigma/Character/Abilities/Enigma Character Weapon Utils")]
    public class EnigmaCharacterWeaponUtils : EnigmaCharacterAbility
    {
        [ReadOnly] [Tooltip("The weapon currently equipped by the Character")]
        public EnigmaWeapon CurrentWeapon;

        protected EnigmaCharacterHandleWeapon _characterHandleWeapon;
      //  protected EnigmaCharacterCastSpell _characterCastSpell;
        
        protected const string _isAttackingAnimationParameterName = "Attacking";
        protected const string _isCastingAnimationParameterName = "Casting";
        protected int _isCastingAnimationParameter;
        protected int _isAttackingAnimationParameter;
        
        // Initialization
        protected override void Initialization()
        {
            base.Initialization();
            _characterHandleWeapon = this.gameObject.GetComponentInParent<EnigmaCharacter>()?.FindAbility<EnigmaCharacterHandleWeapon>();
           // _characterCastSpell = this.gameObject.GetComponentInParent<EnigmaCharacter>()?.FindAbility<EnigmaCharacterCastSpell>();
        }

        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_isAttackingAnimationParameterName, AnimatorControllerParameterType.Bool, out _isAttackingAnimationParameter);
            RegisterAnimatorParameter(_isCastingAnimationParameterName, AnimatorControllerParameterType.Bool, out _isCastingAnimationParameter);
        }

        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _isAttackingAnimationParameter,
                (_characterHandleWeapon.CurrentWeapon.WeaponState.CurrentState != EnigmaWeapon.WeaponStates.WeaponIdle && _characterHandleWeapon.CurrentWeapon.WeaponState.CurrentState != EnigmaWeapon.WeaponStates.WeaponDelayBetweenUses),
                _character._animatorParameters, _character.RunAnimatorSanityChecks);
    
            
            //MMAnimatorExtensions.UpdateAnimatorBool(_animator, _isCastingAnimationParameter, (_characterCastSpell.AbilityCasting), _character._animatorParameters, _character.RunAnimatorSanityChecks);
        }
    }
}