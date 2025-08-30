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
    public sealed class UnitBrain : MonoBehaviour
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

        [Header("Auto-Assign")]
        [SerializeField, Tooltip("Set all child colliders to Ally/Enemy faction layer at Awake/Enable/Start depending on flags.")]
        private bool autoAssignFactionLayer = true;

        [SerializeField] private bool reassignOnEnable = true;
        [SerializeField, Tooltip("If true, minimal nav init runs on Start (does NOT change tunables).")]
        private bool applyNavFromDefinitionOnStart = true;

#if UNITY_EDITOR
        public string CurrentTaskName { get; set; } = "Idle";

        [Header("Debug")]
        public bool DebugDrawCombatGizmos = true;
        public bool DebugAlwaysDraw = false;
        public bool DebugDrawFacing = true;
        public bool DebugDrawSpell = true;

        [SerializeField] private bool logLayerAssignSummary = false;
        [SerializeField] private bool logNavAssignSummary = false;
#endif

        public GameObject CurrentTarget { get; set; }
        public Vector3 CurrentTargetPosition { get; private set; }
        public GameObject CurrentSpellTarget { get; set; }
        public List<GameObject> CurrentSpellTargets { get; set; }
        public Vector3? CurrentSpellTargetPosition { get; set; }
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

            RecomputeMasks();

            if (autoAssignFactionLayer)
                AssignFactionLayers("Awake");

            if (HandleWeapon != null)
            {
                // Use hostiles for scan/selection.
                HandleWeapon.SetTargetLayerMask(_targetMask);
                // Put owner/hurtboxes on own faction layer for any internal filters.
                HandleWeapon.SetDamageableLayer(CombatLayers.FactionLayerIndexFor(_isEnemy));
            }
        }

        private void Start()
        {
            if (autoAssignFactionLayer)
                AssignFactionLayers("Start");

            if (applyNavFromDefinitionOnStart)
                ApplyNavFromDefinition();
        }

        private void OnEnable()
        {
            if (autoAssignFactionLayer && reassignOnEnable)
                AssignFactionLayers("OnEnable");
        }

        private void OnDisable()
        {
            if (_entity != Entity.Null) UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        private void OnDestroy()
        {
            if (_entity != Entity.Null) UnitBrainRegistry.Unregister(_entity, gameObject);
        }

        private void RecomputeMasks()
        {
            _targetMask = CombatLayers.TargetMaskFor(_isEnemy); // == Hostile
        }

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

            if (Character && Character.CharacterModel)
                Character.CharacterModel.layer = layer;

#if UNITY_EDITOR
            if (logLayerAssignSummary)
            {
                string layerName = LayerMask.LayerToName(layer);
                Debug.Log($"[UnitBrain] '{name}' set {changed}/{total} colliders to layer {layer} ({layerName}). Reason={reason}", this);
            }
#endif
        }

        private void ApplyNavFromDefinition()
        {
            if (_navAgent == null) return;

            var body = _navAgent.Body;
            if (body.IsStopped)
            {
                body.IsStopped = false;
                _navAgent.Body = body;
#if UNITY_EDITOR
                if (logNavAssignSummary)
                    Debug.Log($"[UnitBrain] '{name}' nav init: cleared IsStopped on AgentBody.", this);
#endif
            }
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

        public void Setup() { /* hook for future */ }

        public void SetEntity(Entity entity)
        {
            _entity = entity;
            UnitBrainRegistry.Register(entity, this);
        }

        public LayerMask GetHostileLayerMask()   => CombatLayers.HostileMaskFor(_isEnemy);
        public LayerMask GetFriendlyLayerMask()  => CombatLayers.FriendlyMaskFor(_isEnemy);
        public LayerMask GetDamageableLayerMask()=> GetHostileLayerMask();

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

            if (UnitDefinition != null && UnitDefinition.weapon != null && UnitDefinition.weapon.attackRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.7f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.weapon.attackRange);
            }

            if (UnitDefinition != null)
            {
                Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.65f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.stoppingDistance);
            }

            if (UnitDefinition != null && UnitDefinition.autoTargetMinSwitchDistance > 0f)
            {
                Gizmos.color = new Color(0.1f, 0.4f, 1f, 1f);
                UnityEditor.Handles.color = Gizmos.color;
                UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, UnitDefinition.autoTargetMinSwitchDistance);
            }

            if (CurrentTargetPosition != default)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pos, CurrentTargetPosition);
                Gizmos.DrawSphere(CurrentTargetPosition, 0.08f);
            }

            if (CurrentTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, CurrentTarget.transform.position);
                Gizmos.DrawSphere(CurrentTarget.transform.position, 0.07f);
            }

            if (DebugDrawFacing)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pos + Vector3.up * 0.05f, transform.forward * 0.9f);
            }

            if (DebugDrawSpell && UnitDefinition != null && UnitDefinition.unitSpells != null && UnitDefinition.unitSpells.Count > 0)
            {
                var sd = UnitDefinition.unitSpells[0];
                if (sd != null && sd.Range > 0f)
                {
                    Gizmos.color = new Color(sd.DebugColor.r, sd.DebugColor.g, sd.DebugColor.b, 0.35f);
                    UnityEditor.Handles.color = Gizmos.color;
                    UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, sd.Range);
                }
            }
        }
#endif
    }
}
