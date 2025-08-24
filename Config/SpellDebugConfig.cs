// Runtime/AI/Debug/SpellDebugConfig.cs
using UnityEngine;

namespace OneBitRob.AI.Debugging
{
    public enum SpellLogLevel { Off = 0, Warn = 1, Verbose = 2 }

    [CreateAssetMenu(menuName = "TactForge/Config/Debug/Spell Debug Config", fileName = "SpellDebugConfig")]
    public sealed class SpellDebugConfig : ScriptableObject
    {
        public SpellLogLevel logLevel = SpellLogLevel.Warn;

        [Tooltip("Avoids spamming the console with repeated warnings per-frame.")]
        public bool throttleWarnings = true;

        [Tooltip("How often (seconds) Warn messages of the same kind can repeat when throttled.")]
        public float warnRepeatCooldown = 1.0f;
    }
}