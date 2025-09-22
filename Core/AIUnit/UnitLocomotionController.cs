using OneBitRob.AI;
using OneBitRob.Anim;
using ProjectDawn.Navigation.Hybrid;
using UnityEngine;

namespace OneBitRob.EnigmaEngine
{
    public class UnitLocomotionController : EnigmaCharacterAbility
    {
        [Header("Rotation")]
        [Tooltip("Max yaw rotation speed (deg/s).")]
        public float RotationSpeed = 120f;

        [Tooltip("Do not rotate to velocity unless horizontal speed >= this (m/s).")]
        public float MinVelocityToRotate = 0.10f;

        [Tooltip("Snap to target yaw if the remaining error is below this (degrees).")]
        public float SnapIfBelowDeg = 0.5f;

        [Tooltip("How precisely we consider 'aligned' for ForcedRotationTarget (degrees).")]
        public float AlignEpsilonDeg = 1f;

        [Tooltip("The forced facing target set by external systems (clears when aligned).")]
        public Vector3 ForcedRotationTarget;

        private AgentAuthoring _agent;
        private UnitBrain _unitBrain;
        private UnitAnimator _unitAnim;

        protected override void Initialization()
        {
            base.Initialization();
            _agent = this.GetComponentInParent<AgentAuthoring>();
            _unitBrain = this.GetComponentInParent<UnitBrain>();
            _unitAnim = GetComponentInParent<UnitAnimator>();
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
            var movingHorizontally = _agent && (_agent.Body.Velocity.x != 0f || _agent.Body.Velocity.z != 0f);
            var remainingDistance = _agent ? _agent.Body.RemainingDistance : 0f;
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
            if (_character == null) return;

            // --- 1) Try to face horizontal velocity (if meaningful) ---
            if (_agent != null)
            {
                float vx = _agent.Body.Velocity.x;
                float vz = _agent.Body.Velocity.z;
                float speedSq = vx * vx + vz * vz;

                if (speedSq >= (MinVelocityToRotate * MinVelocityToRotate))
                {
                    // Yaw-only target from velocity on XZ plane
                    float targetYaw = Mathf.Atan2(vx, vz) * Mathf.Rad2Deg;
                    ApplyYawTowards(targetYaw);
                    return;
                }
            }

            // --- 2) Otherwise, honor a forced facing target if any ---
            if (ForcedRotationTarget != Vector3.zero)
            {   
                Vector3 dir = ForcedRotationTarget - _character.transform.position;
                dir.y = 0f;
                float magSq = dir.sqrMagnitude;

                if (magSq >= 1e-6f)
                {
                    float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float remaining = Mathf.Abs(Mathf.DeltaAngle(GetCurrentYaw(), targetYaw));

                    ApplyYawTowards(targetYaw);

                    // Clear once tightly aligned
                    if (remaining <= AlignEpsilonDeg)
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

        private float GetCurrentYaw()
        {
            return _character.transform.eulerAngles.y;
        }

        private void ApplyYawTowards(float targetYawDeg)
        {
            float currentYaw = GetCurrentYaw();
            float step = Mathf.Max(0f, RotationSpeed) * Time.deltaTime;

            // Snap to clean up tiny jitter
            float delta = Mathf.DeltaAngle(currentYaw, targetYawDeg);
            if (Mathf.Abs(delta) <= SnapIfBelowDeg)
            {
                _character.transform.rotation = Quaternion.Euler(0f, targetYawDeg, 0f);
                return;
            }

            float nextYaw = Mathf.MoveTowardsAngle(currentYaw, targetYawDeg, step);
            _character.transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
        }

        private void UpdateMovementAnimators()
        {
            var vel = _agent ? new Vector3(_agent.Body.Velocity.x, 0f, _agent.Body.Velocity.z) : Vector3.zero;
            float maxSpeed = _unitBrain ? Mathf.Max(0.01f, _unitBrain.UnitDefinition.moveSpeed) : 4f;
            _unitAnim.ApplyMovement(_movement.CurrentState, vel, maxSpeed);
        }
    }
}
