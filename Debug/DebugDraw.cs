using UnityEngine;

namespace OneBitRob.Debugging
{
    public static class DebugDraw
    {
        private static DebugSettings _settings;

        public static void SetSettings(DebugSettings settings) => _settings = settings;

        private static DebugSettings Settings
        {
            get
            {
                if (_settings) return _settings;
                _settings = ScriptableObject.CreateInstance<DebugSettings>();
                return _settings;
            }
        }

        private static bool ShouldDraw()
        {
            if (!Settings.enableDebugDraws) return false;
            return true;
        }

        public static void Line(Vector3 a, Vector3 b, Color c, float? seconds = null)
        {
            if (!ShouldDraw()) return;
            float dur = Mathf.Max(0f, seconds ?? Settings.defaultDuration);
            Debug.DrawLine(a, b, c, dur, false);
        }

        public static void Ray(Vector3 origin, Vector3 dir, Color c, float? seconds = null)
        {
            if (!ShouldDraw()) return;
            float dur = Mathf.Max(0f, seconds ?? Settings.defaultDuration);
            Debug.DrawRay(origin, dir, c, dur, false);
        }
    }
}
