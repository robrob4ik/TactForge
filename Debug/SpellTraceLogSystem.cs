// Runtime/AI/Systems/SpellTraceLogSystem.cs
using Unity.Entities;
using Unity.Mathematics;
using OneBitRob.AI.Debugging;
using OneBitRob.ECS;
using UnityEngine;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial struct SpellTraceLogSystem : ISystem
    {
        ComponentLookup<SpellConfig> _cfgRO;

        public void OnCreate(ref SystemState state)
        {
            _cfgRO = state.GetComponentLookup<SpellConfig>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _cfgRO.Update(ref state);


            // Log projectile spawns this frame (before bridge consumes)
            foreach (var (req, e) in SystemAPI.Query<RefRO<SpellProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (req.ValueRO.HasValue == 0) continue;
                var r = req.ValueRO;
                Debug.Log($"[Spell] ProjectileSpawn e={e.Index} pos={r.Origin} dir={r.Direction} dmg={r.Damage} maxDist={r.MaxDistance} radius={r.Radius} mask={r.LayerMask} pierce={(r.Pierce==1)}");
            }

            // Log DoTArea spawns this frame
            foreach (var (area, e) in SystemAPI.Query<RefRO<DoTArea>>().WithEntityAccess())
            {
                var a = area.ValueRO;
                // Heuristic: log when freshly set (NextTick is near zero)
                if (a.NextTick <= 0.001f)
                    Debug.Log($"[Spell] DoTArea e={e.Index} pos={a.Position} r={a.Radius} interval={a.Interval} dur={a.Remaining} mask={a.LayerMask} positive={(a.Positive!=0)}");
            }

            // Log chain runner status
            foreach (var (run, e) in SystemAPI.Query<RefRO<SpellChainRunner>>().WithEntityAccess())
            {
                var r = run.ValueRO;
                Debug.Log($"[Spell] Chain e={e.Index} remaining={r.Remaining} fromPos={r.FromPos} nextTarget={r.CurrentTarget.Index} mask={r.LayerMask} speed={r.ProjectileSpeed}");
            }
        }
    }
}
