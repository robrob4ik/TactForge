using System.Collections.Generic;
using OneBitRob.Constants;
using OneBitRob.EnigmaEngine;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitDefinitionProvider))]
    public class UnitBrain : MonoBehaviour
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

            Character = GetComponent<EnigmaCharacter>();
            CombatSubsystem = GetComponent<CombatSubsystem>();
            HandleWeapon = Character ? Character.FindAbility<EnigmaCharacterHandleWeapon>() : null;
            _navMove = Character ? Character.FindAbility<EnigmaCharacterAgentsNavigationMovement>() : null;
            _navAgent = GetComponent<AgentAuthoring>();
            Health = GetComponent<EnigmaHealth>();

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
                HandleWeapon.SetDamageableLayer(_isEnemy ? GameLayers.AllyDamageableLayer : GameLayers.EnemyDamageableLayer);
            }
        }

        private void CacheLayerMasks()
        {
            _targetMask = _isEnemy ? GameLayers.AllyMask : GameLayers.EnemyMask;
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

        public LayerMask GetTargetLayerMask() => _targetMask;
        
        public LayerMask GetDamageableLayerMask()
        {
            int layer = _isEnemy ? GameLayers.AllyDamageableLayer : GameLayers.EnemyDamageableLayer;
            return 1 << layer;
        }

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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (DebugDrawCombatGizmos && DebugAlwaysDraw)
                DrawCombatGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (DebugDrawCombatGizmos)
                DrawCombatGizmos();
        }

        private void DrawCombatGizmos()
        {
            var pos = transform.position;

            // Target detection range
            if (UnitDefinition != null && UnitDefinition.targetDetectionRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.5f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.targetDetectionRange);
            }

            // Weapon attack range
            if (UnitDefinition != null && UnitDefinition.weapon != null && UnitDefinition.weapon.attackRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.7f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.weapon.attackRange);
            }

            // Stopping distance
            if (UnitDefinition != null)
            {
                Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.65f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.stoppingDistance);
            }

            // Retarget hysteresis (autoTargetMinSwitchDistance)
            if (UnitDefinition != null && UnitDefinition.autoTargetMinSwitchDistance > 0f)
            {
                Gizmos.color = new Color(0.35f, 0.6f, 1f, 0.5f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.autoTargetMinSwitchDistance);
            }

            // Desired destination
            if (CurrentTargetPosition != default)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pos, CurrentTargetPosition);
                Gizmos.DrawSphere(CurrentTargetPosition, 0.08f);
            }

            // Current target
            if (CurrentTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, CurrentTarget.transform.position);
                Gizmos.DrawSphere(CurrentTarget.transform.position, 0.07f);
            }

            // Facing
            if (DebugDrawFacing)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pos + Vector3.up * 0.05f, transform.forward * 0.9f);
            }

            // Spell helpers
            if (DebugDrawSpell && UnitDefinition != null && UnitDefinition.unitSpells != null && UnitDefinition.unitSpells.Count > 0)
            {
                var sd = UnitDefinition.unitSpells[0];
                if (sd != null)
                {
                    // Cast range
                    if (sd.Range > 0f)
                    {
                        Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.5f);
                        UnityEditor.Handles.color = Gizmos.color;
                        UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, sd.Range);
                    }

                    // AoE radius (if area or we have an explicit target point)
                    var showAoE = sd.Kind == SpellKind.EffectOverTimeArea || CurrentSpellTargetPosition.HasValue;
                    if (showAoE && sd.AreaRadius > 0f)
                    {
                        Vector3 center = CurrentSpellTargetPosition.HasValue ? CurrentSpellTargetPosition.Value : pos;
                        Gizmos.color = new Color(1f, 0f, 1f, 0.2f);
                        UnityEditor.Handles.color = new Color(1f, 0f, 1f, 0.9f);
                        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, sd.AreaRadius);
                        Gizmos.DrawSphere(center, 0.05f);
                    }
                }
            }
        }
#endif
    }

    public struct UnitBrainTag : IComponentData { }
}
