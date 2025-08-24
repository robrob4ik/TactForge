// Runtime/AI/Brain/UnitBrain.cs
using System.Collections.Generic;
using OneBitRob.Config;
using OneBitRob.EnigmaEngine;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitDefinitionProvider))]
    public sealed partial class UnitBrain : MonoBehaviour
    {
        private Entity _entity;

        public UnitDefinition UnitDefinition { get; private set; }
        private bool _isEnemy;

        public CombatSubsystem CombatSubsystem { get; private set; }
        public EnigmaCharacter Character { get; private set; }
        public EnigmaCharacterHandleWeapon HandleWeapon { get; private set; }
        public EnigmaHealth Health { get; private set; }
        private EnigmaCharacterAgentsNavigationMovement _navMove;
        private AgentAuthoring _navAgent;

        private LayerMask _targetMask;

        public GameObject CurrentTarget { get; set; }
        public Vector3 CurrentTargetPosition { get; private set; }
        public GameObject CurrentSpellTarget { get; set; }
        public List<GameObject> CurrentSpellTargets { get; set; }
        public Vector3? CurrentSpellTargetPosition { get; set; }
        public float NextAllowedAttackTime { get; set; }

#if UNITY_EDITOR
        public string CurrentTaskName { get; set; } = "Idle";

        [Header("Debug")]
        public bool DebugDrawCombatGizmos = true;
        public bool DebugAlwaysDraw = false;
        public bool DebugDrawFacing = true;
        public bool DebugDrawSpell = true;
#endif

        public Entity GetEntity() => _entity;

        private void Awake()
        {
            var defProvider = GetComponent<UnitDefinitionProvider>();
            if (!defProvider || defProvider.unitDefinition == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[{name}] UnitDefinitionProvider or unitDefinition missing.");
#endif
                enabled = false;
                return;
            }

            UnitDefinition = defProvider.unitDefinition;
            _isEnemy = UnitDefinition.isEnemy;

            Character       = GetComponent<EnigmaCharacter>();
            CombatSubsystem = GetComponent<CombatSubsystem>();
            HandleWeapon    = Character ? Character.FindAbility<EnigmaCharacterHandleWeapon>() : null;
            _navMove        = Character ? Character.FindAbility<EnigmaCharacterAgentsNavigationMovement>() : null;
            _navAgent       = GetComponent<AgentAuthoring>();
            Health          = GetComponent<EnigmaHealth>();

            if (Health != null)
            {
                Health.MaximumHealth = UnitDefinition.health;
                Health.InitialHealth = UnitDefinition.health;
                Health.CurrentHealth = UnitDefinition.health;
            }

            CacheLayerMasks();

            if (HandleWeapon != null)
            {
                HandleWeapon.SetTargetLayerMask(_targetMask);
                HandleWeapon.SetDamageableLayer(CombatLayers.DamageableLayerFor(_isEnemy));
            }
        }

        private void OnDisable()
        {
            if (_entity != Entity.Null) UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        private void OnDestroy()
        {
            if (_entity != Entity.Null) UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        private void CacheLayerMasks()
        {
            _targetMask = CombatLayers.TargetMaskFor(_isEnemy);
        }

        public void Setup() { }

        public void SetEntity(Entity entity)
        {
            _entity = entity;
            UnitBrainRegistry.Register(entity, this);
        }

        public LayerMask GetTargetLayerMask() => _targetMask;

        public LayerMask GetDamageableLayerMask() =>
            CombatLayers.DamageableLayerMaskFor(_isEnemy);

        public LayerMask GetFriendlyLayerMask() =>
            CombatLayers.FriendlyLayerMaskFor(_isEnemy);

        public GameObject FindTarget() => CurrentTarget;

        public bool IsFacingTarget()
        {
            if (!CurrentTarget) return false;
            var toTgt = (CurrentTarget.transform.position - transform.position).normalized;
            return Vector3.Dot(transform.forward, toTgt) >= 0.95f;
        }

        public bool IsTargetAlive()
        {
            var targetBrain = CurrentTarget ? CurrentTarget.GetComponent<UnitBrain>() : null;
            if (targetBrain?.Health == null) return false;
            return targetBrain.Character != null
                   && targetBrain.Character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;
        }

        public bool IsTargetInAttackRange(GameObject target)
        {
            if (!target) return false;
            float r = UnitDefinition && UnitDefinition.weapon ? UnitDefinition.weapon.attackRange : 1.5f;
            var diff = transform.position - target.transform.position;
            return diff.sqrMagnitude <= r * r;
        }

        // ─── Locomotion ─────────────────────────────────────────────────────
        public void MoveToPosition(Vector3 position)
        {
            CurrentTargetPosition = position;
            _navAgent?.SetDestinationDeferred(position);
        }

        public void RunTo(Vector3 position) => _navAgent?.SetDestination(position);

        public void SetForcedFacing(Vector3 worldPosition)
        {
            if (_navMove != null) _navMove.ForcedRotationTarget = worldPosition; // smoothed by the ability
        }

        public void RotateToTarget()
        {
            if (CurrentTarget) SetForcedFacing(CurrentTarget.transform.position);
        }

        public void RotateToSpellTarget()
        {
            if (CurrentSpellTarget)
                SetForcedFacing(CurrentSpellTarget.transform.position);
            else if (CurrentSpellTargetPosition.HasValue) SetForcedFacing(CurrentSpellTargetPosition.Value);
        }

        public Vector3 GetCurrentDirection() => transform.forward;

        public bool HasReachedDestination()
        {
            if (_navAgent == null) return true;
            var rd = _navAgent.Body.RemainingDistance;
            if (rd <= 0f) return false;
            return rd <= UnitDefinition.stoppingDistance + 0.001f;
        }

        public float RemainingDistance() => _navAgent ? _navAgent.Body.RemainingDistance : 0f;

        internal class Baker : Baker<UnitBrain>
        {
            public override void Bake(UnitBrain authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<UnitBrainTag>(e);
            }
        }
    }

    public struct UnitBrainTag : IComponentData { }
}
