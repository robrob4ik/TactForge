using UnityEngine;

namespace OneBitRob.Debugging
{
    [CreateAssetMenu(menuName = "TactForge/Config/Debug/Debug Settings", fileName = "DebugSettings")]
    public sealed class DebugSettings : ScriptableObject
    {
        [Header("Visibility")]
        public bool enableDebugDraws = true;
        [Tooltip("Allow DebugDraw in player builds (not just the Editor).")]
        public bool allowInPlayer = false;

        [Header("Style")]
        [Tooltip("Default seconds for lines/rays if not specified by callsites.")]
        [Min(0f)] public float defaultDuration = 1.5f;
        [Tooltip("Draw the same line a few times with tiny vertical offsets for a 'thicker' look.")]
        public bool thickenLines = true;
        [Min(0f)] public float thickenOffset = 0.02f;
        [Range(1, 4)] public int thickenPasses = 2;

        [Header("UI")]
        [Tooltip("Overlay is toggled at runtime with F1; This is just the default state at boot.")]
        public bool showOverlayAtStart = false;
    }
}