using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OneBitRob.Debugging
{
    /// Centralized debug draw facade for runtime (Debug.Draw*) and Scene/Gizmo (Gizmos/Handles).
    /// Uses DebugSettings for behavior (duration, thickness, depth test).
    /// Now robust even if no DebugSettings asset is injected/loaded.
    public static class DebugDraw
    {
        private static DebugSettings _settings;

        private const float DEFAULT_DURATION = 0f;        // one frame
        private const bool  DEFAULT_DEPTH_TEST = true;
        private const float DEFAULT_LINE_THICKNESS = 1.5f;
        private const float DEFAULT_DISC_THICKNESS = 1.5f;
        private const int   DEFAULT_DISC_SEGMENTS = 64;

        /// <summary>Inject settings at runtime/editor if you don’t want to rely on Resources.</summary>
        public static void SetSettings(DebugSettings settings) => _settings = settings;

        // Null-safe helpers
        private static bool  Enabled                   => _settings?.enableDebugDraws ?? true;
        private static float Duration(float? v)        => v ?? (_settings?.defaultDuration      ?? DEFAULT_DURATION);
        private static bool  Depth(bool? v)            => v ?? (_settings?.depthTest            ?? DEFAULT_DEPTH_TEST);
        private static float LineThickness(float? v)   => v ?? (_settings?.defaultLineThickness ?? DEFAULT_LINE_THICKNESS);
        private static float DiscThickness(float? v)   => v ?? (_settings?.defaultDiscThickness ?? DEFAULT_DISC_THICKNESS);

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void AutoLoadSettings()
        {
            if (_settings == null)
            {
                _settings = Resources.Load<DebugSettings>("CFG_DebugDraw");
            }
        }
#endif

        private static bool ShouldDraw() => Enabled;

        /// <summary>Runtime line (Game/Scene view). Thickness unsupported at runtime.</summary>
        public static void Line(Vector3 start, Vector3 end, Color color, float? duration = null, bool? depthTest = null)
        {
            if (!ShouldDraw()) return;
            Debug.DrawLine(start, end, color, Duration(duration), Depth(depthTest));
        }

        /// <summary>Runtime ray (Game/Scene view). Thickness unsupported at runtime.</summary>
        public static void Ray(Vector3 origin, Vector3 direction, Color color, float? duration = null, bool? depthTest = null)
        {
            if (!ShouldDraw()) return;
            Debug.DrawRay(origin, direction, color, Duration(duration), Depth(depthTest));
        }

        /// <summary>Draw a line in the Scene view. Editor supports thickness; runtime falls back to Gizmos line.</summary>
        public static void GizmoLine(Vector3 start, Vector3 end, Color color, float? thickness = null)
        {
            if (!ShouldDraw()) return;

            var prev = Handles.color;
            Handles.color = color;

            float t = LineThickness(thickness);
            if (t > 0f)
                Handles.DrawAAPolyLine(t, new[] { start, end });
            else
                Handles.DrawLine(start, end);

            Handles.color = prev;

        }

        /// <summary>Draw a ray in the Scene view. Editor thickness supported via GizmoLine.</summary>
        public static void GizmoRay(Vector3 origin, Vector3 direction, Color color, float length, float? thickness = null)
        {
            if (!ShouldDraw()) return;

            if (direction.sqrMagnitude < 1e-6f) return;
            var end = origin + direction.normalized * Mathf.Max(0f, length);
            GizmoLine(origin, end, color, thickness);
        }

        /// <summary>
        /// Draw a disc on the XZ plane.
        /// Editor uses Handles.DrawWireDisc (thickness supported),
        /// runtime approximates with segments using Gizmos.
        /// </summary>
        public static void DiscXZ(Vector3 center, float radius, Color color, float? thickness = null, int? segmentsOverride = null)
        {
            if (!ShouldDraw() || radius <= 0f) return;

            var prev = Handles.color;
            Handles.color = color;
            float t = DiscThickness(thickness);
            Handles.DrawWireDisc(center, Vector3.up, radius, t);
            Handles.color = prev;
        }

        /// <summary>Solid sphere gizmo at position.</summary>
        public static void GizmoSphere(Vector3 center, float radius, Color color)
        {
            if (!ShouldDraw()) return;
            var prev = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = prev;
        }

        /// <summary>Wireframe sphere gizmo at position.</summary>
        public static void GizmoWireSphere(Vector3 center, float radius, Color color)
        {
            if (!ShouldDraw()) return;
            var prev = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.color = prev;
        }

        /// <summary>Small point helper (solid sphere) for anchors/targets.</summary>
        public static void Point(Vector3 center, float radius, Color color)
        {
            GizmoSphere(center, radius, color);
        }
    }
}
