using MoreMountains.Tools;
using OneBitRob.AI;
using ProjectDawn.Navigation.Hybrid;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    [AddComponentMenu("Enigma Engine/Enigma Character Agents Navigation Movement")]
    public class EnigmaCharacterAgentsNavigationMovement : EnigmaCharacterAbility
    {
        [Tooltip("How quickly the model rotates towards velocity (deg/s)")]
        public float RotationSpeed = 720f;

        [Tooltip("The forced rotation applied by an external script")]
        public Vector3 ForcedRotationTarget;

        private AgentAuthoring _agent;
        private UnitBrain _unitBrain;

        protected const string _walkingAnimationParameterName = "Walking";
        protected const string _combatStanceAnimationParameterName = "CombatStance";
        protected const string _idleAnimationParameterName = "Idle";

        protected int _walkingAnimationParameter;
        protected int _combatStanceAnimationParameter;
        protected int _idleAnimationParameter;

        protected override void Initialization()
        {
            base.Initialization();
            _agent = this.GetComponentInParent<AgentAuthoring>();
            _unitBrain = this.GetComponentInParent<UnitBrain>();
        }

        public override void ProcessAbility()
        {
            base.ProcessAbility();
            processCharacterMovement();
            processCharacterRotation();
        }

        private void processCharacterMovement()
        {
            var positiveVelocity = _agent.Body.Velocity.x != 0 || _agent.Body.Velocity.z != 0;
            var remainingDistance = _agent.Body.RemainingDistance;
            
            switch (_movement.CurrentState)
            {
                case EnigmaCharacterStates.MovementStates.Walking:
                    if (!positiveVelocity)
                    {
                        EnigmaLogger.Log(_character.name + " - Changing state to Idle");
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                        UpdateMovementAnimators();
                        break;
                    }

                    if (remainingDistance < _unitBrain.UnitDefinition.combatStanceDistance)
                    {
                        EnigmaLogger.Log(_character.name + " - Changing state to CombatStance");
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.CombatStance);
                        UpdateMovementAnimators();
                    }

                    break;


                case EnigmaCharacterStates.MovementStates.Idle:
                    if (positiveVelocity && remainingDistance < _unitBrain.UnitDefinition.combatStanceDistance)
                    {
                        EnigmaLogger.Log(_character.name + " - Changing state to CombatStance");
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.CombatStance);
                        UpdateMovementAnimators();
                        break;
                    }

                    if (positiveVelocity)
                    {
                        EnigmaLogger.Log(_character.name + " - Changing state to Idle");
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.Walking);
                        UpdateMovementAnimators();
                    }

                    break;

                case EnigmaCharacterStates.MovementStates.CombatStance:
                    if (remainingDistance > _unitBrain.UnitDefinition.combatStanceDistance && positiveVelocity)
                    {
                        EnigmaLogger.Log(_character.name + " - Changing state to Walking");
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.Walking);
                        UpdateMovementAnimators();
                        break;
                    }

                    if (!positiveVelocity)
                    {
                        EnigmaLogger.Log(_character.name + " - Changing state to Idle");
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                        UpdateMovementAnimators();
                        break;
                    }

                    break;
                
                default: 
                    _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                    UpdateMovementAnimators();
                    break;
            }
        }

        private void processCharacterRotation()
        {
            var positiveVelocity = _agent.Body.Velocity.x != 0 || _agent.Body.Velocity.z != 0;
            if (positiveVelocity)
            {
                Vector3 vel = new Vector3(_agent.Body.Velocity.x, 0f, _agent.Body.Velocity.z);
                Quaternion targetRot = Quaternion.LookRotation(vel, Vector3.up);
                _character.transform.rotation = Quaternion.RotateTowards(_character.transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
            }
            else if (ForcedRotationTarget != Vector3.zero)
            {
                Vector3 dirToTarget = ForcedRotationTarget - _character.transform.position;
                dirToTarget.y = 0f;
                Quaternion targetRotation = Quaternion.LookRotation(dirToTarget, Vector3.up);
                _character.transform.rotation = Quaternion.RotateTowards(_model.transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);
                if (_character.transform.rotation == targetRotation) ForcedRotationTarget = Vector3.zero;
            }
        }


        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_walkingAnimationParameterName, AnimatorControllerParameterType.Bool, out _walkingAnimationParameter);
            RegisterAnimatorParameter(_idleAnimationParameterName, AnimatorControllerParameterType.Bool, out _idleAnimationParameter);
            RegisterAnimatorParameter(_combatStanceAnimationParameterName, AnimatorControllerParameterType.Bool, out _combatStanceAnimationParameter);
        }

        private void UpdateMovementAnimators()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _walkingAnimationParameter, (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Walking), _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _idleAnimationParameter, (_movement.CurrentState == EnigmaCharacterStates.MovementStates.Idle), _character._animatorParameters, _character.RunAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _combatStanceAnimationParameter, (_movement.CurrentState == EnigmaCharacterStates.MovementStates.CombatStance), _character._animatorParameters, _character.RunAnimatorSanityChecks);
        }
    }
}