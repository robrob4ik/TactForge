// Runtime/AI/Debug/SpellDebug.cs
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.AI.Debugging
{
    public static class SpellDebug
    {
        private static SpellDebugConfig _config;
        private static readonly Dictionary<string, float> _lastWarnTime = new();

        private static SpellDebugConfig Config
        {
            get
            {
                if (_config == null)
                    _config = Resources.Load<SpellDebugConfig>("SpellDebugSettings");
                return _config;
            }
        }

        public static bool Verbose => Config != null && Config.logLevel == SpellLogLevel.Verbose;
        public static bool WarnOn   => Config != null && Config.logLevel >= SpellLogLevel.Warn;

        public static void LogVerbose(string msg, Object ctx = null)
        {
            if (Verbose) Debug.Log(msg, ctx);
        }

        public static void LogWarnThrottled(string key, string msg, Object ctx = null)
        {
            if (!WarnOn) return;

            var s = Config;
            if (s == null || !s.throttleWarnings)
            {
                Debug.LogWarning(msg, ctx);
                return;
            }

            float now = Time.unscaledTime;
            if (_lastWarnTime.TryGetValue(key, out var t) && now - t < Mathf.Max(0.05f, s.warnRepeatCooldown))
                return;

            _lastWarnTime[key] = now;
            Debug.LogWarning(msg, ctx);
        }
    }
}