// NEW FILE: Assets/PROJECT/Scripts/Editor/Debug/DoTAreaDebugDrawSystem.cs
#if UNITY_EDITOR
using OneBitRob.ECS;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.AI.Debugging
{
    /// <summary>Draws a simple wire ring for every active DoTArea.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial struct DoTAreaDebugDrawSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var area in SystemAPI.Query<RefRO<DoTArea>>())
            {
                var a = area.ValueRO;
                if (a.Radius <= 0f) continue;

                float3 center = a.Position + new float3(0f, math.max(0.02f, a.VfxYOffset * 0.6f), 0f);
                Color c = (a.Positive != 0) ? new Color(0.2f, 1f, 0.35f, 0.8f) : new Color(1f, 0.25f, 0.25f, 0.9f);
                DrawRing(center, a.Radius, c);
            }
        }

        private static void DrawRing(float3 center, float radius, Color color)
        {
            const int segs = 32;
            var c = (Vector3)center;
            Vector3 prev = c + Vector3.right * radius;
            for (int i = 1; i <= segs; i++)
            {
                float t = (i / (float)segs) * 2f * Mathf.PI;
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                Debug.DrawLine(prev, p, color, 0f, false);
                prev = p;
            }
        }
    }
}
#endif