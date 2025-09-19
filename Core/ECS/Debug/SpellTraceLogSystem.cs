// File: OneBitRob/AI/SpellTraceLogSystem.cs
using Unity.Entities;
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

            // Projectiles emitted this frame (check enablement, not HasValue)
            foreach (var (reqRO, e) in SystemAPI.Query<RefRO<SpellProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (!SystemAPI.IsComponentEnabled<SpellProjectileSpawnRequest>(e)) continue;
                var r = reqRO.ValueRO;
                Debug.Log($"[Spell] ProjectileSpawn e={e.Index} pos={r.Origin} dir={r.Direction} dmg={r.Damage} maxDist={r.MaxDistance} radius={r.Radius} mask={r.LayerMask} pierce={(r.Pierce==1)}");
            }

            // DoT areas: log first‑frame set (heuristic)
            foreach (var (areaRO, e) in SystemAPI.Query<RefRO<DoTArea>>().WithEntityAccess())
            {
                var a = areaRO.ValueRO;
                if (a.NextTick <= 0.001f)
                    Debug.Log($"[Spell] DoTArea e={e.Index} pos={a.Position} r={a.Radius} interval={a.Interval} dur={a.Remaining} mask={a.LayerMask} positive={(a.Positive!=0)}");
            }

            // Chain runner status (informational)
            foreach (var (runRO, e) in SystemAPI.Query<RefRO<SpellChainRunner>>().WithEntityAccess())
            {
                var r = runRO.ValueRO;
                Debug.Log($"[Spell] Chain e={e.Index} remaining={r.Remaining} fromPos={r.FromPos} nextTarget={r.CurrentTarget.Index} mask={r.LayerMask} speed={r.ProjectileSpeed}");
            }
        }
    }
}