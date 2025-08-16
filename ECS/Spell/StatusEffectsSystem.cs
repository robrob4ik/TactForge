// FILE: OneBitRob/ECS/StatusEffectsSystem.cs
using OneBitRob.AI;
using OneBitRob.FX; // ← added
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct StatusEffectsSystem : ISystem
    {
        static readonly Collider[] s_Cols = new Collider[256];

        public void OnCreate(ref SystemState state)
        {
            // Intentionally not using RequireForUpdate here; we want to run whenever any exists.
        }

        public void OnUpdate(ref SystemState state)
        {
            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            // ───────────────────────────────────────────────────────── Target DoT/HoT
            foreach (var (dot, e) in SystemAPI.Query<RefRW<DotOnTarget>>().WithEntityAccess())
            {
                var d = dot.ValueRO;

                d.Remaining -= SystemAPI.Time.DeltaTime;
                if (d.Remaining <= 0f) { ecb.RemoveComponent<DotOnTarget>(e); continue; }

                if (d.NextTick <= now)
                {
                    var tb = UnitBrainRegistry.Get(d.Target);
                    if (tb != null && tb.Health != null)
                    {
                        // Positive flag means "healing over time"
                        bool isHot = d.Positive != 0;
                        float shownAmount = Mathf.Abs(d.AmountPerTick);

                        if (isHot)
                        {
                            // Heal via negative damage in your health impl; keep as-is.
                            float amt = -shownAmount;
                            tb.Health.Damage(amt, tb.gameObject, 0f, 0f, Vector3.zero);

                            DamageNumbersManager.Popup(new DamageNumbersParams
                            {
                                Kind     = DamagePopupKind.Hot,
                                Follow   = tb.transform,
                                Position = tb.transform.position,
                                Amount   = shownAmount
                            });
                        }
                        else
                        {
                            tb.Health.Damage(shownAmount, tb.gameObject, 0f, 0f, Vector3.zero);

                            DamageNumbersManager.Popup(new DamageNumbersParams
                            {
                                Kind     = DamagePopupKind.Dot,
                                Follow   = tb.transform,
                                Position = tb.transform.position,
                                Amount   = shownAmount
                            });
                        }
                    }
                    d.NextTick = now + math.max(0.05f, d.Interval);
                }

                dot.ValueRW = d;
            }

            // ───────────────────────────────────────────────────────── Area DoT/HoT
            foreach (var (area, e) in SystemAPI.Query<RefRW<DoTArea>>().WithEntityAccess())
            {
                var a = area.ValueRO;

                a.Remaining -= SystemAPI.Time.DeltaTime;
                if (a.Remaining <= 0f) { ecb.RemoveComponent<DoTArea>(e); continue; }

                if (a.NextTick <= now)
                {
                    int count = Physics.OverlapSphereNonAlloc(
                        (Vector3)a.Position, a.Radius, s_Cols, a.LayerMask, QueryTriggerInteraction.Collide);

                    bool isHot = a.Positive != 0;
                    float shownAmount = Mathf.Abs(a.AmountPerTick);

                    for (int i = 0; i < count; i++)
                    {
                        var col = s_Cols[i];
                        if (!col) continue;
                        var tb = col.GetComponentInParent<UnitBrain>();
                        if (tb == null || tb.Health == null) continue;

                        if (isHot)
                        {
                            float amt = -shownAmount;
                            tb.Health.Damage(amt, tb.gameObject, 0f, 0f, Vector3.zero);
                            DamageNumbersManager.Popup(new DamageNumbersParams
                            {
                                Kind     = DamagePopupKind.Hot,
                                Follow   = tb.transform,
                                Position = tb.transform.position,
                                Amount   = shownAmount
                            });
                        }
                        else
                        {
                            tb.Health.Damage(shownAmount, tb.gameObject, 0f, 0f, Vector3.zero);
                            DamageNumbersManager.Popup(new DamageNumbersParams
                            {
                                Kind     = DamagePopupKind.Dot,
                                Follow   = tb.transform,
                                Position = tb.transform.position,
                                Amount   = shownAmount
                            });
                        }
                    }

                    a.NextTick = now + math.max(0.05f, a.Interval);
                }

                area.ValueRW = a;
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}
