using UnityEngine;

namespace OneBitRob.Debugging
{
    [CreateAssetMenu(menuName = "TactForge/Config/Debug Settings", fileName = "DebugSettings")]
    public sealed class DebugSettings : ScriptableObject
    {
        [Header("Visibility")]
        public bool enableDebugDraws = true;

        [Header("Style / Timing")]
        [Tooltip("Default seconds for Debug.DrawLine/DrawRay if not specified by callsites.")]
        [Min(0f)] public float defaultDuration = 0f;

        [Header("Thickness (Editor 'Handles' only)")]
        [Tooltip("Default line thickness in Scene view (pixels). Used by GizmoLine/GizmoRay in Editor.")]
        [Min(0f)] public float defaultLineThickness = 2f;

        [Tooltip("Default disc thickness in Scene view (pixels). Used by DiscXZ in Editor.")]
        [Min(0f)] public float defaultDiscThickness = 2f;

        [Header("Runtime Fallbacks")]
        [Tooltip("Circle segment count used when Handles are unavailable (e.g., player builds).")]
        [Range(8, 256)] public int circleSegments = 64;

        [Tooltip("Default depthTest for Debug.DrawLine/DrawRay in Game/Scene view.")]
        public bool depthTest = false;
    }
}