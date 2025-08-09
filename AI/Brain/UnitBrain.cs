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
    [RequireComponent(typeof(UnitDefinitionProvider))]
    public class UnitBrain : MonoBehaviour
    {
        internal ITargetingStrategy TargetingStrategy => _targetingStrategy;

        // Entity & ECS
        private Entity _entity;
        private World _world;
        private EntityManager _entityManager;

        // Definition and config
        public UnitDefinition UnitDefinition { get; private set; }

        // Core systems
        public CombatSubsystem CombatSubsystem { get; private set; }
        public EnigmaCharacter Character { get; private set; }
        public EnigmaCharacterHandleWeapon HandleWeapon { get; private set; }
        public EnigmaHealth Health { get; private set; }

        public float NextAllowedAttackTime { get; set; }
        public string CurrentTaskName { get; set; } = "Idle";

        // AI & Navigation
        public ITargetingStrategy _targetingStrategy;
        private ICombatStrategy _combatStrategy;
        private AgentAuthoring _navigationAgent;

        // Abilities
        private EnigmaCharacterAgentsNavigationMovement _characterAgentsNavigationMovement;
        private EnigmaCharacterCastSpell _characterCastSpell;

        // Runtime state (Mono-side convenience)
        public GameObject CurrentTarget { get; set; }
        public Vector3 CurrentTargetPosition { get; private set; }

        public GameObject CurrentSpellTarget { get; set; }
        public List<GameObject> CurrentSpellTargets { get; set; }
        public Vector3? CurrentSpellTargetPosition { get; set; }

        public Entity GetEntity() => _entity;

        private void Awake()
        {
            // Load Definitions
            UnitDefinition = GetComponent<UnitDefinitionProvider>().unitDefinition;

            // Subsystems
            CombatSubsystem = GetComponent<CombatSubsystem>();

            // Character & Abilities
            Character = GetComponent<EnigmaCharacter>();
            HandleWeapon = Character.FindAbility<EnigmaCharacterHandleWeapon>();
            _characterAgentsNavigationMovement = Character.FindAbility<EnigmaCharacterAgentsNavigationMovement>();
            _characterCastSpell = Character.FindAbility<EnigmaCharacterCastSpell>();
            _navigationAgent = GetComponent<AgentAuthoring>();

            // Configure Weapon Target Layer
            HandleWeapon.SetTargetLayerMask(UnitDefinition.isEnemy ? GameLayers.AllyMask : GameLayers.EnemyMask);
            HandleWeapon.SetDamageableLayer(UnitDefinition.isEnemy ? GameLayers.AllyDamageableLayer : GameLayers.EnemyDamageableLayer);

            // Default spell if available
            if (UnitDefinition.unitSpells.Count > 0)
                _characterCastSpell.CurrentSpell = UnitDefinition.unitSpells[0];

            // Strategies
            _combatStrategy = CombatStrategyFactory.GetStrategy(UnitDefinition.combatStrategy);
            _targetingStrategy = TargetingStrategyFactory.GetStrategy(UnitDefinition.targetingStrategy);

            // Health
            Health = GetComponent<EnigmaHealth>();
            Health.MaximumHealth = UnitDefinition.health;
            Health.InitialHealth = UnitDefinition.health;
            Health.CurrentHealth = UnitDefinition.health;

            _world = World.DefaultGameObjectInjectionWorld;
            _entityManager = _world.EntityManager;
        }

        public void Setup()
        {
            // Placeholder: hook up per‑unit initialization here if needed.
        }

        public void SetEntity(Entity entity)
        {
            _entity = entity;
            UnitBrainRegistry.Register(entity, this);
        }

        private void OnDestroy()
        {
            if (_entity != Entity.Null)
                UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        // Target helpers
        public LayerMask GetTargetLayerMask() => UnitDefinition.isEnemy ? GameLayers.AllyMask : GameLayers.EnemyMask;
        public LayerMask GetDetectionLayerMask() => UnitDefinition.isEnemy ? GameLayers.EnemyDetectionMask : GameLayers.AllyDetectionMask;
        public LayerMask GetAlliesLayerMask() => UnitDefinition.isEnemy ? GameLayers.EnemyMask : GameLayers.AllyMask;

        public GameObject FindTarget() => CurrentTarget;

        public bool IsFacingTarget()
        {
            if (!CurrentTarget) return false;
            var toTgt = (CurrentTarget.transform.position - transform.position).normalized;
            var dot = Vector3.Dot(transform.forward, toTgt);
            // Tighten/loosen as you prefer; this is ~18°
            return dot >= 0.95f;
        }

        public bool IsTargetAlive()
        {
            var character = CurrentTarget?.MMGetComponentNoAlloc<EnigmaCharacter>();
            return character != null && character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;
        }

        public bool IsTargetInAttackRange(GameObject target)
        {
            var diff = transform.position - target.transform.position;
            return diff.sqrMagnitude <= UnitDefinition.attackRange * UnitDefinition.attackRange;
        }

        // Locomotion (called by bridge)
        public void MoveToPosition(Vector3 position)
        {
            CurrentTargetPosition = position;
            _navigationAgent.SetDestinationDeferred(position);
        }

        public void RunTo(Vector3 position) => _navigationAgent.SetDestination(position);

        public void RotateToTarget()
        {
            if (!CurrentTarget) return;
            _characterAgentsNavigationMovement.ForcedRotationTarget = CurrentTarget.transform.position;
        }

        public void RotateToSpellTarget()
        {
            // BUGFIX: previously rotated to CurrentTarget instead of the spell’s targeting point
            if (CurrentSpellTarget)
            {
                _characterAgentsNavigationMovement.ForcedRotationTarget = CurrentSpellTarget.transform.position;
            }
            else if (CurrentSpellTargetPosition.HasValue)
            {
                _characterAgentsNavigationMovement.ForcedRotationTarget = CurrentSpellTargetPosition.Value;
            }
        }

        public Vector3 GetCurrentDirection() => transform.forward;

        public bool HasReachedDestination()
        {
            var rd = _navigationAgent.Body.RemainingDistance;
            if (rd <= 0f) return false;
            return rd <= UnitDefinition.stoppingDistance + 0.001f;
        }

        public float RemainingDistance() => _navigationAgent.Body.RemainingDistance;

        // Combat
        public void Attack(Transform target) => _combatStrategy.Attack(this, target);
        public void AimAtTarget(Transform target) => CombatSubsystem.AimAtTarget(target);

        // Spells
        public bool CanCastSpell() => _characterCastSpell is not null && _characterCastSpell.CanCast();
        public bool ReadyToCastSpell() => _characterCastSpell is not null && _characterCastSpell.ReadyToCast();

        public bool TryCastSpell()
        {
            if (UnitDefinition.unitSpells.Count == 0 || _characterCastSpell == null) return false;

            var spell = UnitDefinition.unitSpells[0];
            switch (spell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    if (CurrentSpellTarget == null) return false;
                    return _characterCastSpell.TryCastSpell(CurrentSpellTarget);

                case SpellTargetType.MultiTarget:
                    if (CurrentSpellTargets == null || CurrentSpellTargets.Count == 0) return false;
                    return _characterCastSpell.TryCastSpell(null, CurrentSpellTargets);

                case SpellTargetType.AreaOfEffect:
                    if (!CurrentSpellTargetPosition.HasValue) return false;
                    return _characterCastSpell.TryCastSpell(null, null, CurrentSpellTargetPosition.Value);
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
            // Draw aim/target and useful radii
            var pos = transform.position;

            // Detection range
            if (UnitDefinition != null && UnitDefinition.targetDetectionRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.5f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.targetDetectionRange);
            }

            // Attack range
            if (UnitDefinition != null && UnitDefinition.attackRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.7f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.attackRange);
            }

            // Stopping distance
            if (UnitDefinition != null)
            {
                Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.65f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.stoppingDistance);
            }

            // Desired destination (cyan)
            if (CurrentTargetPosition != default)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pos, CurrentTargetPosition);
                Gizmos.DrawSphere(CurrentTargetPosition, 0.1f);
            }

            // Current target line (green)
            if (CurrentTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, CurrentTarget.transform.position);
                Gizmos.DrawSphere(CurrentTarget.transform.position, 0.07f);
            }

            // Spell target (magenta)
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

    public struct UnitBrainTag : IComponentData { }
}
