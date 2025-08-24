// FILE: Assets/PROJECT/Scripts/Runtime/AI/Brain/UnitBrain.LayerAssign.cs
// Part of UnitBrain (partial). Assigns own faction layer to all child colliders.
using UnityEngine;
using OneBitRob.Config;

namespace OneBitRob.AI
{
    public sealed partial class UnitBrain : MonoBehaviour
    {
        [Header("Faction Layer Auto-Assign")]
        [SerializeField] private bool _reassignOnEnable = true;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool _logLayerAssignSummary = false;
#endif

        private void Start()
        {
            if (autoAssignFactionLayer)
                AssignFactionLayers_Internal("Start");
        }

        private void OnEnable()
        {
            if (autoAssignFactionLayer && _reassignOnEnable)
                AssignFactionLayers_Internal("OnEnable");
        }

        public void ReapplyFactionLayers()
        {
            if (autoAssignFactionLayer)
                AssignFactionLayers_Internal("Manual");
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
    }
}