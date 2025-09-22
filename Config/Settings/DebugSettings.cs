using UnityEngine;

namespace OneBitRob.Debugging
{
    [CreateAssetMenu(menuName = "TactForge/Config/Debug Settings", fileName = "DebugSettings")]
    public sealed class DebugSettings : ScriptableObject
    {
        [Header("Visibility")]
        public bool enableDebugDraws = true;

        [Header("Style / Timing")]
        [Min(0f)] public float defaultDuration = 0f;

        [Header("Thickness (Editor 'Handles' only)")]
        [Min(0f)] public float defaultLineThickness = 2f;

        [Min(0f)] public float defaultDiscThickness = 2f;

        [Header("Runtime Fallbacks")]
        [Range(8, 256)] public int circleSegments = 64;

        [Tooltip("Default depthTest for Debug.DrawLine/DrawRay in Game/Scene view.")]
        public bool depthTest = false;
    }
}