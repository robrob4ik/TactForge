using System.Collections.Generic;
using OneBitRob.Config;
using OneBitRob.Debugging;
using OneBitRob.EnigmaEngine;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitDefinitionProvider))]
    public sealed class UnitBrain : MonoBehaviour
    {
        private Entity _entity;

        public UnitDefinition UnitDefinition { get; private set; }
        private bool _isEnemy;

        public UnitCombatController UnitCombatController { get; private set; }
        public EnigmaCharacter Character { get; private set; }
        public EnigmaCharacterHandleWeapon HandleWeapon { get; private set; }
        public EnigmaHealth Health { get; private set; }
        private EnigmaCharacterAgentsNavigationMovement _navMove;
        private AgentAuthoring _navAgent;

        private LayerMask _targetMask;

        [Header("Auto-Assign")]
        [SerializeField, Tooltip("Set all child colliders to Ally/Enemy faction layer at Awake/Enable/Start depending on flags.")]
        private bool autoAssignFactionLayer = true;

        [SerializeField]
        private bool reassignOnEnable = true;

        [SerializeField, Tooltip("If true, minimal nav init runs on Start (does NOT change tunables).")]
        private bool applyNavFromDefinitionOnStart = true;

#if UNITY_EDITOR
        public string CurrentTaskName { get; set; } = "Idle";

        [Header("Debug")]
        public bool DebugDrawCombatGizmos = true;

        public bool DebugAlwaysDraw = false;
        public bool DebugDrawFacing = true;
        public bool DebugDrawSpell = true;

#endif

        public GameObject CurrentTarget { get; set; }
        public Vector3 CurrentTargetPosition { get; private set; }
        public float NextAllowedAttackTime { get; set; }

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
            UnitCombatController = GetComponent<UnitCombatController>();
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

            RecomputeMasks();

            if (autoAssignFactionLayer) AssignFactionLayers("Awake");

            if (HandleWeapon != null)
            {
                HandleWeapon.SetTargetLayerMask(_targetMask);
                HandleWeapon.SetDamageableLayer(CombatLayers.FactionLayerIndexFor(_isEnemy));
            }
        }

        private void Start()
        {
            if (autoAssignFactionLayer) AssignFactionLayers("Start");

            if (applyNavFromDefinitionOnStart) ApplyNavFromDefinition();
        }

        private void OnEnable()
        {
            if (autoAssignFactionLayer && reassignOnEnable) AssignFactionLayers("OnEnable");
        }

        private void OnDisable()
        {
            if (_entity != Entity.Null) UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        private void OnDestroy()
        {
            if (_entity != Entity.Null) UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        private void RecomputeMasks() { _targetMask = CombatLayers.TargetMaskFor(_isEnemy); }

        private void AssignFactionLayers(string reason)
        {
            int layer = CombatLayers.FactionLayerIndexFor(_isEnemy);
            if (layer < 0 || layer > 31) return;

            var colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            int total = 0, changed = 0;

            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (!c) continue;
                total++;
                if (c.gameObject.layer != layer)
                {
                    c.gameObject.layer = layer;
                    changed++;
                }
            }

            if (Character && Character.CharacterModel) Character.CharacterModel.layer = layer;

#if UNITY_EDITOR
            string layerName = LayerMask.LayerToName(layer);
            Debug.Log($"[UnitBrain] '{name}' set {changed}/{total} colliders to layer {layer} ({layerName}). Reason={reason}", this);
#endif
        }

        private void ApplyNavFromDefinition()
        {
            if (_navAgent == null) return;

            var body = _navAgent.Body;
 
            // TODO FIX HOW TO?

            if (body.IsStopped)
            {
                body.IsStopped = false;
            }

            _navAgent.Body = body;

        }

        public void StopAgentMotion()
        {
            if (_navAgent == null) return;

            var body = _navAgent.Body;
            if (!body.IsStopped)
            {
                body.Stop();
                _navAgent.Body = body;
            }
        }

        public bool IsTargetAlive()
        {
            var targetBrain = CurrentTarget ? CurrentTarget.GetComponent<UnitBrain>() : null;
            if (targetBrain?.Health == null) return false;
            return targetBrain.Character != null
                   && targetBrain.Character.ConditionState.CurrentState != EnigmaCharacterStates.CharacterConditions.Dead;
        }

        public void MoveToPosition(Vector3 position)
        {
            CurrentTargetPosition = position;
            _navAgent?.SetDestinationDeferred(position);
        }

        public void SetForcedFacing(Vector3 worldPosition)
        {
            if (_navMove != null) _navMove.ForcedRotationTarget = worldPosition;
        }

        public float RemainingDistance() => _navAgent ? _navAgent.Body.RemainingDistance : 0f;

        public void Setup()
        {
            /* hook for future */
        }

        public void SetEntity(Entity entity)
        {
            _entity = entity;
            UnitBrainRegistry.Register(entity, this);
        }

        public LayerMask GetHostileLayerMask() => CombatLayers.HostileMaskFor(_isEnemy);
        public LayerMask GetFriendlyLayerMask() => CombatLayers.FriendlyMaskFor(_isEnemy);
        public LayerMask GetDamageableLayerMask() => GetHostileLayerMask();


#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (DebugDrawCombatGizmos && DebugAlwaysDraw) DrawCombatGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (DebugDrawCombatGizmos) DrawCombatGizmos();
        }

        private void DrawCombatGizmos()
        {
            var pos = transform.position;

            // Primary weapon range disc
            if (UnitDefinition != null && UnitDefinition.weapon && UnitDefinition.weapon.attackRange > 0f)
            {
                DebugDraw.DiscXZ(pos, UnitDefinition.weapon.attackRange, Color.red);
            }

            // Auto target min-switch distance disc
            if (UnitDefinition != null && UnitDefinition.autoTargetMinSwitchDistance > 0f)
            {
                DebugDraw.DiscXZ(pos, UnitDefinition.autoTargetMinSwitchDistance, Color.blue);
            }

            // Target position and direct target
            if (CurrentTargetPosition != default)
            {
                DebugDraw.GizmoLine(pos, CurrentTargetPosition, Color.cyan);
            }

            if (CurrentTarget)
            {
                var tpos = CurrentTarget.transform.position;
                DebugDraw.GizmoLine(pos, tpos, Color.orange);
            }

            // Facing
            if (DebugDrawFacing)
            {
                DebugDraw.GizmoRay(pos + Vector3.up * 0.05f, transform.forward, Color.yellow, 0.9f);
            }

            // Spell #0 range disc
            if (DebugDrawSpell && UnitDefinition != null && UnitDefinition.unitSpells != null && UnitDefinition.unitSpells.Count > 0)
            {
                var sd = UnitDefinition.unitSpells[0];
                if (sd != null && sd.Range > 0f)
                {
                    DebugDraw.DiscXZ(pos, sd.Range, Color.darkRed);
                }
            }
        }
#endif
    }
}
