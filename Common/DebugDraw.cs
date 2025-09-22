using UnityEditor;
using UnityEngine;

namespace OneBitRob.Debugging
{
    public static class DebugDraw
    {
        private static DebugSettings _settings;

        private const float DEFAULT_DURATION = 0f;
        private const bool  DEFAULT_DEPTH_TEST = true;
        private const float DEFAULT_LINE_THICKNESS = 1.5f;
        private const float DEFAULT_DISC_THICKNESS = 1.5f;

        public static void SetSettings(DebugSettings settings) => _settings = settings;

        private static bool  Enabled                   => _settings?.enableDebugDraws ?? false;
        private static float Duration(float? v)        => v ?? (_settings?.defaultDuration      ?? DEFAULT_DURATION);
        private static bool  Depth(bool? v)            => v ?? (_settings?.depthTest            ?? DEFAULT_DEPTH_TEST);
        private static float LineThickness(float? v)   => v ?? (_settings?.defaultLineThickness ?? DEFAULT_LINE_THICKNESS);
        private static float DiscThickness(float? v)   => v ?? (_settings?.defaultDiscThickness ?? DEFAULT_DISC_THICKNESS);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static bool _complainedOnce;
#endif
        private static bool ShouldDraw()
        {
            if (_settings == null)
            {
                return false;
            }
            return Enabled;
        }

        public static void Line(Vector3 start, Vector3 end, Color color, float? duration = null, bool? depthTest = null)
        {
            if (!ShouldDraw()) return;
            Debug.DrawLine(start, end, color, Duration(duration), Depth(depthTest));
        }

        public static void Ray(Vector3 origin, Vector3 direction, Color color, float? duration = null, bool? depthTest = null)
        {
            if (!ShouldDraw()) return;
            Debug.DrawRay(origin, direction, color, Duration(duration), Depth(depthTest));
        }

        public static void GizmoLine(Vector3 start, Vector3 end, Color color, float? thickness = null)
        {
            if (!ShouldDraw()) return;
            var prev = Handles.color;
            Handles.color = color;
            float t = LineThickness(thickness);
            if (t > 0f) Handles.DrawAAPolyLine(t, new[] { start, end });
            else Handles.DrawLine(start, end);
            Handles.color = prev;
        }

        public static void GizmoRay(Vector3 origin, Vector3 direction, Color color, float length, float? thickness = null)
        {
            if (!ShouldDraw()) return;
            if (direction.sqrMagnitude < 1e-6f) return;
            var end = origin + direction.normalized * Mathf.Max(0f, length);
            GizmoLine(origin, end, color, thickness);
        }

        public static void DiscXZ(Vector3 center, float radius, Color color, float? thickness = null, int? segmentsOverride = null)
        {
            if (!ShouldDraw() || radius <= 0f) return;
            var prev = Handles.color;
            Handles.color = color;
            float t = DiscThickness(thickness);
            Handles.DrawWireDisc(center, Vector3.up, radius, t);
            Handles.color = prev;
        }

        public static void GizmoSphere(Vector3 center, float radius, Color color)
        {
            if (!ShouldDraw()) return;
            var prev = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = prev;
        }

        public static void GizmoWireSphere(Vector3 center, float radius, Color color)
        {
            if (!ShouldDraw()) return;
            var prev = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.color = prev;
        }

        public static void Point(Vector3 center, float radius, Color color) => GizmoSphere(center, radius, color);
        
        public static void WireSphereXYZ(Vector3 center, float radius, Color color, int segments = 24)
        {
            if (!ShouldDraw() || radius <= 0f || segments < 3) return;

            // XY plane
            {
                Vector3 prev = center + Vector3.right * radius;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (i / (float)segments) * 2f * Mathf.PI;
                    Vector3 p = center + new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
                    Line(prev, p, color);
                    prev = p;
                }
            }

            // XZ plane
            {
                Vector3 prev = center + Vector3.forward * radius;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (i / (float)segments) * 2f * Mathf.PI;
                    Vector3 p = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                    Line(prev, p, color);
                    prev = p;
                }
            }

            // YZ plane
            {
                Vector3 prev = center + Vector3.up * radius;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (i / (float)segments) * 2f * Mathf.PI;
                    Vector3 p = center + new Vector3(0f, Mathf.Cos(t) * radius, Mathf.Sin(t) * radius);
                    Line(prev, p, color);
                    prev = p;
                }
            }
        }
    }
}
