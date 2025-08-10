// FILE: OneBitRob/AI/UnitBrain.cs

using System;
using System.Collections.Generic;
using MoreMountains.Tools;
using OneBitRob.Constants;
using OneBitRob.EnigmaEngine;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitDefinitionProvider))]
    public class UnitBrain : MonoBehaviour
    {
        // Strategies
        internal ITargetingStrategy TargetingStrategy => _targetingStrategy;

        // ECS identity
        private Entity _entity;

        // Definition / config
        public UnitDefinition UnitDefinition { get; private set; }
        private bool _isEnemy;

        // Core subsystems (cached once)
        public CombatSubsystem CombatSubsystem { get; private set; }
        public EnigmaCharacter Character { get; private set; }
        public EnigmaCharacterHandleWeapon HandleWeapon { get; private set; }
        public EnigmaHealth Health { get; private set; }
        private EnigmaCharacterAgentsNavigationMovement _navMove;
        private EnigmaCharacterCastSpell _castSpell;
        private AgentAuthoring _navAgent;

        // Strategies
        private ITargetingStrategy _targetingStrategy;

        // Cached layer masks
        private LayerMask _targetMask;
        private LayerMask _detectionMask;
        private LayerMask _alliesMask;

        // Runtime state
        public GameObject CurrentTarget { get; set; }
        public Vector3 CurrentTargetPosition { get; private set; }
        public GameObject CurrentSpellTarget { get; set; }
        public List<GameObject> CurrentSpellTargets { get; set; }
        public Vector3? CurrentSpellTargetPosition { get; set; }
        public float NextAllowedAttackTime { get; set; }
#if UNITY_EDITOR
        public string CurrentTaskName { get; set; } = "Idle";
#endif

        public Entity GetEntity() => _entity;

        private void Awake()
        {
            // Load definitions
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

            // Subsystems / abilities
            Character = GetComponent<EnigmaCharacter>();
            CombatSubsystem = GetComponent<CombatSubsystem>();
            HandleWeapon = Character ? Character.FindAbility<EnigmaCharacterHandleWeapon>() : null;
            _navMove = Character ? Character.FindAbility<EnigmaCharacterAgentsNavigationMovement>() : null;
            _castSpell = Character ? Character.FindAbility<EnigmaCharacterCastSpell>() : null;
            _navAgent = GetComponent<AgentAuthoring>();
            Health = GetComponent<EnigmaHealth>();

            // Health init
            if (Health != null)
            {
                Health.MaximumHealth = UnitDefinition.health;
                Health.InitialHealth = UnitDefinition.health;
                Health.CurrentHealth = UnitDefinition.health;
            }

            // Targeting strategy
            _targetingStrategy = TargetingStrategyFactory.GetStrategy(UnitDefinition.targetingStrategy);

            // Weapon + masks
            CacheLayerMasks();
            if (HandleWeapon != null)
            {
                HandleWeapon.SetTargetLayerMask(_targetMask);
                HandleWeapon.SetDamageableLayer(_isEnemy ? GameLayers.AllyDamageableLayer : GameLayers.EnemyDamageableLayer);
            }

            // Default spell
            if (_castSpell != null && UnitDefinition.unitSpells != null && UnitDefinition.unitSpells.Count > 0) _castSpell.CurrentSpell = UnitDefinition.unitSpells[0];
        }

        private void CacheLayerMasks()
        {
            _targetMask = _isEnemy ? GameLayers.AllyMask : GameLayers.EnemyMask;
            _detectionMask = _isEnemy ? GameLayers.EnemyDetectionMask : GameLayers.AllyDetectionMask;
            _alliesMask = _isEnemy ? GameLayers.EnemyMask : GameLayers.AllyMask;
        }

        public void Setup() { }

        public void SetEntity(Entity entity)
        {
            _entity = entity;
            UnitBrainRegistry.Register(entity, this);
        }

        private void OnDestroy()
        {
            if (_entity != Entity.Null) UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        // ─── Target helpers & masks ──────────────────────────────────────────
        public LayerMask GetTargetLayerMask() => _targetMask;
        public LayerMask GetDetectionLayerMask() => _detectionMask;
        public LayerMask GetAlliesLayerMask() => _alliesMask;
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
            if (_navMove != null) _navMove.ForcedRotationTarget = worldPosition;
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

        // ─── Combat ─────────────────────────────────────────────────────────
        public void Attack(Transform target) => EcsAttackDispatcher.Request(this, target);

        public void AimAtTarget(Transform target)
        {
            if (target != null) SetForcedFacing(target.position); // hand off to AgentsNavigation / orientation
        }

        // ─── Spells ─────────────────────────────────────────────────────────
        public bool CanCastSpell() => _castSpell != null && _castSpell.CanCast();
        public bool ReadyToCastSpell() => _castSpell != null && _castSpell.ReadyToCast();

        public bool TryCastSpell()
        {
            if (_castSpell == null || UnitDefinition.unitSpells == null || UnitDefinition.unitSpells.Count == 0) return false;

            var spell = UnitDefinition.unitSpells[0];
            switch (spell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    if (!CurrentSpellTarget) return false;
                    return _castSpell.TryCastSpell(CurrentSpellTarget);
                case SpellTargetType.MultiTarget:
                    if (CurrentSpellTargets == null || CurrentSpellTargets.Count == 0) return false;
                    return _castSpell.TryCastSpell(null, CurrentSpellTargets);
                case SpellTargetType.AreaOfEffect:
                    if (!CurrentSpellTargetPosition.HasValue) return false;
                    return _castSpell.TryCastSpell(null, null, CurrentSpellTargetPosition.Value);
            }

            return false;
        }

        internal class Baker : Baker<UnitBrain>
        {
            public override void Bake(UnitBrain authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<UnitBrainTag>(e);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var pos = transform.position;

            if (UnitDefinition != null && UnitDefinition.targetDetectionRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.5f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.targetDetectionRange);
            }

            if (UnitDefinition != null && UnitDefinition.weapon != null && UnitDefinition.weapon.attackRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.7f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.weapon.attackRange);

                if (UnitDefinition.weapon is RangedWeaponDefinition rw)
                {
                    // optional viz for muzzle
                    var fwd = transform.forward;
                    Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
                    Gizmos.DrawLine(pos, pos + fwd * rw.muzzleForward);
                }
                else if (UnitDefinition.weapon is MeleeWeaponDefinition mw)
                {
                    var half = mw.halfAngleDeg;
                    var fwd = transform.forward;
                    var left = Quaternion.AngleAxis(-half, Vector3.up) * fwd;
                    var right = Quaternion.AngleAxis(half, Vector3.up) * fwd;

                    UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                    UnityEditor.Handles.DrawSolidArc(pos, Vector3.up, left, half * 2f, UnitDefinition.weapon.attackRange);
                    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
                    Gizmos.DrawLine(pos, pos + left * UnitDefinition.weapon.attackRange);
                    Gizmos.DrawLine(pos, pos + right * UnitDefinition.weapon.attackRange);
                }
            }

            if (UnitDefinition != null)
            {
                Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.65f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.stoppingDistance);
            }

            if (CurrentTargetPosition != default)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pos, CurrentTargetPosition);
                Gizmos.DrawSphere(CurrentTargetPosition, 0.1f);
            }

            if (CurrentTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, CurrentTarget.transform.position);
                Gizmos.DrawSphere(CurrentTarget.transform.position, 0.07f);
            }

            if (CurrentSpellTarget)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(pos, CurrentSpellTarget.transform.position);
                Gizmos.DrawSphere(CurrentSpellTarget.transform.position, 0.08f);
            }
            else if (CurrentSpellTargetPosition.HasValue)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(CurrentSpellTargetPosition.Value, 0.08f);
            }
        }
#endif
    }

    public struct UnitBrainTag : IComponentData
    {
    }
}