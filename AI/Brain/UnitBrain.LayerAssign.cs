// Runtime/AI/Brain/UnitBrain.LayerAssign.cs
// Part of UnitBrain (partial). Adds auto-assignment of Damageable layers to all child colliders.

using UnityEngine;

namespace OneBitRob.AI
{
    public sealed partial class UnitBrain : MonoBehaviour
    {
        [Header("Damageable Layer Auto-Assign")]
        [Tooltip("Automatically put all child colliders on the correct Damageable layer based on UnitDefinition.isEnemy.")]
        [SerializeField] private bool _autoAssignDamageableLayer = true;

        [Tooltip("Include trigger colliders in reassignment (recommended ON).")]
        [SerializeField] private bool _assignTriggers = true;

        [Tooltip("Include non-trigger colliders in reassignment.")]
        [SerializeField] private bool _assignNonTriggers = true;

        [Tooltip("Re-apply when the object is enabled (useful for pooled units).")]
        [SerializeField] private bool _reassignOnEnable = true;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool _logLayerAssignSummary = false;
#endif

        // Runs after Awake(), before first Update. Safe place to finalize collider layers.
        private void Start()
        {
            if (_autoAssignDamageableLayer)
                AssignDamageableLayers_Internal("Start");
        }

        // Helps with pooled objects: if this object is disabled/enabled, ensure layers are still correct.
        private void OnEnable()
        {
            if (_autoAssignDamageableLayer && _reassignOnEnable)
                AssignDamageableLayers_Internal("OnEnable");
        }

        /// <summary>Call this if you changed faction at runtime (rare) or want to force a refresh.</summary>
        public void ReapplyDamageableLayers()
        {
            if (_autoAssignDamageableLayer)
                AssignDamageableLayers_Internal("Manual");
        }

        // Internal helper: sets all child colliders to the correct Damageable layer.
        private void AssignDamageableLayers_Internal(string reason)
        {
            if (!UnitDefinition)
                return;

            int layer = OneBitRob.Config.CombatLayers.DamageableLayerFor(UnitDefinition.isEnemy);

            var colliders = GetComponentsInChildren<Collider>(true);
            int totalConsidered = 0;
            int changed = 0;

            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (!c) continue;

                if (c.isTrigger && !_assignTriggers) continue;
                if (!c.isTrigger && !_assignNonTriggers) continue;

                totalConsidered++;

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
                Debug.Log($"[UnitBrain] '{name}' set {changed}/{totalConsidered} colliders to layer {layer} ({layerName}). Reason={reason}", this);
            }
#endif
        }
    }
}
