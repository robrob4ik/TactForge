using System;
using System.Collections.Generic;
using MoreMountains.Tools;
using OneBitRob.Constants;
using OneBitRob.EnigmaEngine;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
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

        // Runtime state
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
            HandleWeapon.SetTargetLayerMask(UnitDefinition.isEnemy ? GameLayers.AllyMask :  GameLayers.EnemyMask);
            HandleWeapon.SetDamageableLayer(UnitDefinition.isEnemy ? GameLayers.AllyDamageableLayer : GameLayers.EnemyDamageableLayer);

            // Assign default spell if available
            if (UnitDefinition.unitSpells.Count > 0) { _characterCastSpell.CurrentSpell = UnitDefinition.unitSpells[0]; }

            // Instantiate Strategies
            _combatStrategy = CombatStrategyFactory.GetStrategy(UnitDefinition.combatStrategy);
            _targetingStrategy = TargetingStrategyFactory.GetStrategy(UnitDefinition.targetingStrategy);
            
            // Set Health
            Health = GetComponent<EnigmaHealth>();
            Health.MaximumHealth = UnitDefinition.health;
            Health.InitialHealth = UnitDefinition.health;
            Health.CurrentHealth = UnitDefinition.health;
            
            _world = World.DefaultGameObjectInjectionWorld;
            _entityManager = _world.EntityManager;
            
        }

        public void Setup() { }

        public void SetEntity(Entity entity)
        {
            _entity = entity;
            UnitBrainRegistry.Register(entity, this);
        }

        void OnDestroy()
        {
            if (_entity != Entity.Null)
                UnitBrainRegistry.Unregister(_entity);
        }

        // Target
        public LayerMask GetTargetLayerMask() { return UnitDefinition.isEnemy ? GameLayers.AllyMask : GameLayers.EnemyMask; }
        public LayerMask GetDetectionLayerMask() { return UnitDefinition.isEnemy ? GameLayers.EnemyDetectionMask : GameLayers.AllyDetectionMask; }
        public LayerMask GetAlliesLayerMask() { return UnitDefinition.isEnemy ? GameLayers.EnemyMask : GameLayers.AllyMask; }

        public GameObject FindTarget()
        {
            //CurrentTarget = _targetingStrategy?.GetTarget(this, TargetSubsystem.PotentialTargets, true);;

            return CurrentTarget;
        }

        public bool IsFacingTarget() { return true; }

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

        // Locomotion
        public void MoveToPosition(Vector3 position)
        {
            CurrentTargetPosition = position;
            _navigationAgent.SetDestinationDeferred(position);
        }

        public void RunTo(Vector3 position)
        {
            _navigationAgent.SetDestination(position); // TODO
        }

        public void RotateToTarget() { _characterAgentsNavigationMovement.ForcedRotationTarget = CurrentTarget.transform.position; }
        
        public void RotateToSpellTarget() { _characterAgentsNavigationMovement.ForcedRotationTarget = CurrentTarget.transform.position; }

        public Vector3 GetCurrentDirection() { return transform.forward; }

        public bool HasReachedDestination() { return _navigationAgent.Body.RemainingDistance != 0 && _navigationAgent.Body.RemainingDistance <= UnitDefinition.stoppingDistance; }

        
        public float RemainingDistance() { return _navigationAgent.Body.RemainingDistance; }
        
        // Combat
        public void Attack(Transform target) { _combatStrategy.Attack(this, target); }

        public void AimAtTarget(Transform target) { CombatSubsystem.AimAtTarget(target); }

        // Spells
        public bool CanCastSpell() { return _characterCastSpell is not null && _characterCastSpell.CanCast(); }

        public bool ReadyToCastSpell() { return _characterCastSpell is not null && _characterCastSpell.ReadyToCast(); }

        public bool TryCastSpell()
        {
            if (UnitDefinition.unitSpells.Count == 0 || _characterCastSpell == null) return false;

            var spell = UnitDefinition.unitSpells[0];
            Debug.Log($"Casting spell {spell.name} --!!!!!!!!- ");
            Debug.Log(CurrentSpellTarget);
            Debug.Log(CurrentSpellTargets);
            Debug.Log(CurrentSpellTargetPosition);
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
    }

    public struct UnitBrainTag : IComponentData
    {
    }

    public struct UnitBrainRef : ISharedComponentData, IEquatable<UnitBrainRef>
    {
        public UnitBrain Value;
        public bool Equals(UnitBrainRef other) => Value == other.Value;
        public override int GetHashCode() => Value ? Value.GetHashCode() : 0;
    }
}