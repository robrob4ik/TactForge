using UnityEngine;

namespace OneBitRob.Debugging
{
    /// Drop-in wrapper for Debug.DrawLine/Ray that obeys DebugSettings and draws thicker, longer lines by default.
    public static class DebugDraw
    {
        private static DebugSettings _settings;

        // Can be called from MainGameManager or any bootstrap
        public static void SetSettings(DebugSettings settings) => _settings = settings;

        private static DebugSettings Settings
        {
            get
            {
                if (_settings) return _settings;
                // Lazy default to keep things working even without an asset
                _settings = ScriptableObject.CreateInstance<DebugSettings>();
                return _settings;
            }
        }

        private static bool ShouldDraw()
        {
#if UNITY_EDITOR
            if (!Settings.enableDebugDraws) return false;
            return true;
#else
            return Settings.enableDebugDraws && Settings.allowInPlayer;
#endif
        }

        public static void Line(Vector3 a, Vector3 b, Color c, float? seconds = null)
        {
            if (!ShouldDraw()) return;

            float dur = Mathf.Max(0f, seconds ?? Settings.defaultDuration);

            if (!Settings.thickenLines)
            {
                Debug.DrawLine(a, b, c, dur, false);
                return;
            }

            // Fake thickness by slight vertical offsets
            float step = Mathf.Max(0f, Settings.thickenOffset);
            int passes = Mathf.Clamp(Settings.thickenPasses, 1, 4);
            for (int i = 0; i < passes; i++)
            {
                float y = i * step;
                Debug.DrawLine(a + Vector3.up * y, b + Vector3.up * y, c, dur, false);
            }
        }

        public static void Ray(Vector3 origin, Vector3 dir, Color c, float? seconds = null)
        {
            if (!ShouldDraw()) return;

            float dur = Mathf.Max(0f, seconds ?? Settings.defaultDuration);
            if (!Settings.thickenLines)
            {
                Debug.DrawRay(origin, dir, c, dur, false);
                return;
            }

            float step = Mathf.Max(0f, Settings.thickenOffset);
            int passes = Mathf.Clamp(Settings.thickenPasses, 1, 4);
            for (int i = 0; i < passes; i++)
            {
                float y = i * step;
                Debug.DrawRay(origin + Vector3.up * y, dir, c, dur, false);
            }
        }
    }
}
