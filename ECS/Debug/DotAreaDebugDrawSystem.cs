#if UNITY_EDITOR
using OneBitRob.Debugging;
using OneBitRob.ECS;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.AI.Debugging
{
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
                Color c = (a.Positive != 0) ? new Color(0.2f, 1f, 0.35f, 0.9f) : new Color(1f, 0.25f, 0.25f, 0.95f);
                DrawRing(center, a.Radius, c, 0.4f);
            }
        }

        private static void DrawRing(float3 center, float radius, Color color, float duration)
        {
            const int segs = 32;
            var c = (Vector3)center;
            Vector3 prev = c + Vector3.right * radius;
            for (int i = 1; i <= segs; i++)
            {
                float t = (i / (float)segs) * 2f * Mathf.PI;
                Vector3 p = c + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                DebugDraw.Line(prev, p, color);
                prev = p;
            }
        }
    }
}
#endif