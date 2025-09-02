using UnityEngine;

namespace OneBitRob.Debugging
{
    [CreateAssetMenu(menuName = "TactForge/Config/Debug Settings", fileName = "DebugSettings")]
    public sealed class DebugSettings : ScriptableObject
    {
        [Header("Visibility")]
        public bool enableDebugDraws = true;

        [Header("Style")]
        [Tooltip("Default seconds for lines/rays if not specified by callsites.")]
        [Min(0f)] public float defaultDuration = 1.5f;
    }
}