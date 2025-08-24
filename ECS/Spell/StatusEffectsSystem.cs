// FILE: Assets/PROJECT/Scripts/ECS/Spell/StatusEffectsSystem.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct StatusEffectsSystem : ISystem
    {
        static readonly Collider[] s_Cols = new Collider[256];

        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            // ───────── Target DoT/HoT
            foreach (var (dot, e) in SystemAPI.Query<RefRW<DotOnTarget>>().WithEntityAccess())
            {
                var d = dot.ValueRO;

                d.Remaining -= SystemAPI.Time.DeltaTime;
                if (d.Remaining <= 0f) { ecb.RemoveComponent<DotOnTarget>(e); continue; }

                if (d.NextTick <= now)
                {
                    var tb = OneBitRob.AI.UnitBrainRegistry.Get(d.Target);
#if UNITY_EDITOR
                    if (tb == null)
                        Debug.LogWarning("[Spells] DotOnTarget tick: target brain missing.");
#endif
                    if (tb != null && tb.Health != null)
                    {
                        bool isHot = d.Positive != 0;
                        float shownAmount = Mathf.Abs(d.AmountPerTick);

                        OneBitRob.FX.SpellVfxPoolManager.PlayByHash(d.EffectVfxIdHash, tb.transform.position, tb.transform);

                        float amt = isHot ? -shownAmount : shownAmount;
                        tb.Health.Damage(amt, tb.gameObject, 0f, 0f, Vector3.zero);

                        OneBitRob.FX.DamageNumbersManager.Popup(new OneBitRob.FX.DamageNumbersParams
                        {
                            Kind     = isHot ? OneBitRob.FX.DamagePopupKind.Hot : OneBitRob.FX.DamagePopupKind.Dot,
                            Follow   = tb.transform,
                            Position = tb.transform.position,
                            Amount   = shownAmount
                        });
                    }
                    d.NextTick = now + max(0.05f, d.Interval);
                }

                dot.ValueRW = d;
            }

            // ───────── Area DoT/HoT (persistent VFX)
            foreach (var (area, e) in SystemAPI.Query<RefRW<DoTArea>>().WithEntityAccess())
            {
                var a = area.ValueRO;

                if (a.AreaVfxIdHash != 0)
                {
                    // Use only long operands in the XOR mix
                    long salt   = unchecked((long)0x9E3779B97F4A7C15UL);
                    long keyLo  = ((long)e.Index << 32) | (long)(uint)e.Version;
                    long key    = keyLo ^ (((long)a.AreaVfxIdHash) << 1) ^ salt;

                    if (!em.HasComponent<ActiveAreaVfx>(e))
                    {
                        OneBitRob.FX.SpellVfxPoolManager.BeginPersistentByHash(a.AreaVfxIdHash, key, (Vector3)a.Position, null);
                        ecb.AddComponent(e, new ActiveAreaVfx { Key = key, IdHash = a.AreaVfxIdHash });
                    }
                    else
                    {
                        OneBitRob.FX.SpellVfxPoolManager.MovePersistent(key, (Vector3)a.Position, null);
                    }
                }
                else if (em.HasComponent<ActiveAreaVfx>(e))
                {
                    var av = em.GetComponentData<ActiveAreaVfx>(e);
                    OneBitRob.FX.SpellVfxPoolManager.EndPersistent(av.Key);
                    ecb.RemoveComponent<ActiveAreaVfx>(e);
                }

                a.Remaining -= SystemAPI.Time.DeltaTime;
                if (a.Remaining <= 0f)
                {
                    if (em.HasComponent<ActiveAreaVfx>(e))
                    {
                        var av = em.GetComponentData<ActiveAreaVfx>(e);
                        OneBitRob.FX.SpellVfxPoolManager.EndPersistent(av.Key);
                        ecb.RemoveComponent<ActiveAreaVfx>(e);
                    }
                    ecb.RemoveComponent<DoTArea>(e);
                    continue;
                }

                if (a.NextTick <= now)
                {
                    int count = Physics.OverlapSphereNonAlloc(
                        (Vector3)a.Position, a.Radius, s_Cols, a.LayerMask, QueryTriggerInteraction.Collide);

#if UNITY_EDITOR
                    if (count == 0)
                    {
                        string maskNames = "";
                        for (int l = 0; l < 32; l++)
                            if (((1 << l) & a.LayerMask) != 0)
                                maskNames += (maskNames.Length > 0 ? "," : "") + LayerMask.LayerToName(l);
                        Debug.Log($"[Spells] DoTArea tick at {a.Position} (r={a.Radius}) hit 0 colliders. LayerMask={a.LayerMask} ({maskNames})");
                    }
#endif

                    bool isHot = a.Positive != 0;
                    float shownAmount = Mathf.Abs(a.AmountPerTick);

                    for (int i = 0; i < count; i++)
                    {
                        var col = s_Cols[i];
                        if (!col) continue;
                        var tb = col.GetComponentInParent<OneBitRob.AI.UnitBrain>();
                        if (tb == null || tb.Health == null) continue;

                        float amt = isHot ? -shownAmount : shownAmount;
                        tb.Health.Damage(amt, tb.gameObject, 0f, 0f, Vector3.zero);

                        OneBitRob.FX.DamageNumbersManager.Popup(new OneBitRob.FX.DamageNumbersParams
                        {
                            Kind     = isHot ? OneBitRob.FX.DamagePopupKind.Hot : OneBitRob.FX.DamagePopupKind.Dot,
                            Follow   = tb.transform,
                            Position = tb.transform.position,
                            Amount   = shownAmount
                        });
                    }

                    a.NextTick = now + max(0.05f, a.Interval);
                }

                area.ValueRW = a;
            }

            // Cleanup orphaned FX if DoTArea was removed elsewhere
            foreach (var (av, e) in SystemAPI.Query<RefRO<ActiveAreaVfx>>().WithNone<DoTArea>().WithEntityAccess())
            {
                OneBitRob.FX.SpellVfxPoolManager.EndPersistent(av.ValueRO.Key);
                ecb.RemoveComponent<ActiveAreaVfx>(e);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}
