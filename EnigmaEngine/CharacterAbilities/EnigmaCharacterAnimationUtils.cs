using UnityEngine;
using MoreMountains.Tools;
using OneBitRob.AI;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma/Character/Abilities/Enigma Character Animation Utils")]
    public class EnigmaCharacterAnimationUtils : EnigmaCharacterAbility
    {
        private UnitBrain _brain;
        
        private int _movementTypeAnimationParameter;
        private const string _movementTypeParameterName = "MovementType";
        
        protected override void Initialization()
        {
            _brain = GetComponentInParent<UnitBrain>();
            RegisterAnimatorParameter(_movementTypeParameterName, AnimatorControllerParameterType.Bool, out _movementTypeAnimationParameter);
            MMAnimatorExtensions.UpdateAnimatorInteger(_animator, _movementTypeAnimationParameter, (int)_brain.UnitDefinition.movementType, _character._animatorParameters, _character.RunAnimatorSanityChecks);
        }
    }
}