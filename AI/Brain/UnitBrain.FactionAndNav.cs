// FILE: Assets/PROJECT/Scripts/Runtime/AI/Brain/UnitBrain.FactionAndNav.cs
// Summary: faction auto-assign kept; nav "apply" no longer writes Body.Speed etc.
// Adds StopAgentMotion() that uses AgentBody.Stop() safely.

using UnityEngine;
using OneBitRob.Config;
using ProjectDawn.Navigation.Hybrid; // AgentAuthoring

namespace OneBitRob.AI
{
    public sealed partial class UnitBrain : MonoBehaviour
    {
        [Header("Faction Layer Auto-Assign")]
        [SerializeField] private bool _reassignOnEnable = true;

        [Header("Nav Auto-Config")]
        [Tooltip("If true, minimal nav init runs on Start (does NOT touch locomotion tunables).")]
        [SerializeField] private bool _applyNavFromDefinitionOnStart = true;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool _logLayerAssignSummary = false;
        [SerializeField] private bool _logNavAssignSummary = false;
#endif

        private void Start()
        {
            if (autoAssignFactionLayer)
                AssignFactionLayers_Internal("Start");

            if (_applyNavFromDefinitionOnStart)
                ApplyNavFromDefinition_Internal();
        }

        private void OnEnable()
        {
            if (autoAssignFactionLayer && _reassignOnEnable)
                AssignFactionLayers_Internal("OnEnable");
        }

        private void AssignFactionLayers_Internal(string reason)
        {
            if (!UnitDefinition) return;

            int layer = CombatLayers.FactionLayerIndexFor(UnitDefinition.isEnemy);

            var colliders = GetComponentsInChildren<Collider>(true);
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

#if UNITY_EDITOR
            if (_logLayerAssignSummary)
            {
                string layerName = LayerMask.LayerToName(layer);
                Debug.Log($"[UnitBrain] '{name}' set {changed}/{total} colliders to layer {layer} ({layerName}). Reason={reason}", this);
            }
#endif
        }

        private void ApplyNavFromDefinition_Internal()
        {
            if (_navAgent == null) return;

            var body = _navAgent.Body;
            body.IsStopped = false;
            _navAgent.Body = body;

#if UNITY_EDITOR
            if (_logNavAssignSummary)
                Debug.Log($"[UnitBrain] '{name}' nav init: cleared IsStopped on AgentBody.", this);
#endif
        }

        public void StopAgentMotion()
        {
            if (_navAgent == null) return;

            var body = _navAgent.Body;
            body.Stop();            // sets IsStopped = true and Velocity = 0
            _navAgent.Body = body;  // write back to ECS
        }
    }
}
