// FILE: OneBitRob/EnigmaEngine/EnigmaCharacterAgentsNavigationMovement.cs
using MoreMountains.Tools;
using OneBitRob;
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
            ProcessCharacterMovement();
            ProcessCharacterRotation();
        }

        private float GetCombatStanceDistance()
        {
            // Enter stance slightly before actual attack range so units settle nicely
            var weapon = _unitBrain != null && _unitBrain.UnitDefinition != null ? _unitBrain.UnitDefinition.weapon : null;
            float range = (weapon != null) ? Mathf.Max(0.01f, weapon.attackRange) : 1.5f;
            return Mathf.Max(1.0f, range * 0.9f);
        }

        private void ProcessCharacterMovement()
        {
            var movingHorizontally = _agent.Body.Velocity.x != 0f || _agent.Body.Velocity.z != 0f;
            var remainingDistance = _agent.Body.RemainingDistance;
            float combatStanceDistance = GetCombatStanceDistance();

            switch (_movement.CurrentState)
            {
                case EnigmaCharacterStates.MovementStates.Walking:
                    if (!movingHorizontally)
                    {
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.Idle);
                        UpdateMovementAnimators();
                        break;
                    }

                    if (remainingDistance < combatStanceDistance)
                    {
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.CombatStance);
                        UpdateMovementAnimators();
                    }
                    break;

                case EnigmaCharacterStates.MovementStates.Idle:
                    if (movingHorizontally && remainingDistance < combatStanceDistance)
                    {
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.CombatStance);
                        UpdateMovementAnimators();
                        break;
                    }

                    if (movingHorizontally)
                    {
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.Walking);
                        UpdateMovementAnimators();
                    }
                    break;

                case EnigmaCharacterStates.MovementStates.CombatStance:
                    if (remainingDistance > combatStanceDistance && movingHorizontally)
                    {
                        _movement.ChangeState(EnigmaCharacterStates.MovementStates.Walking);
                        UpdateMovementAnimators();
                        break;
                    }

                    if (!movingHorizontally)
                    {
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

        private void ProcessCharacterRotation()
        {
            // Rotate to movement if moving
            var vx = _agent.Body.Velocity.x;
            var vz = _agent.Body.Velocity.z;
            bool hasVelocity = (vx * vx + vz * vz) > 0f;

            if (hasVelocity)
            {
                Vector3 vel = new Vector3(vx, 0f, vz);
                Quaternion targetRot = Quaternion.LookRotation(vel, Vector3.up);
                _character.transform.rotation = Quaternion.RotateTowards(
                    _character.transform.rotation,
                    targetRot,
                    RotationSpeed * Time.deltaTime
                );
                return;
            }

            // Else honor a forced rotation target if any
            if (ForcedRotationTarget != Vector3.zero)
            {
                Vector3 dir = ForcedRotationTarget - _character.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    _character.transform.rotation = Quaternion.RotateTowards(
                        _character.transform.rotation,
                        targetRot,
                        RotationSpeed * Time.deltaTime
                    );

                    // Clear once aligned (~1 degree)
                    if (Quaternion.Angle(_character.transform.rotation, targetRot) < 1f)
                    {
                        ForcedRotationTarget = Vector3.zero;
                    }
                }
                else
                {
                    ForcedRotationTarget = Vector3.zero;
                }
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
