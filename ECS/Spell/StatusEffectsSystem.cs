using OneBitRob.FX;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using OneBitRob.VFX;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct StatusEffectsSystem : ISystem
    {
        static readonly Collider[] s_Cols = new Collider[256];

        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            // Target DoT/HoT (persistent target VFX)
            foreach (var (dotRW, caster) in SystemAPI.Query<RefRW<DotOnTarget>>().WithEntityAccess())
            {
                var dot = dotRW.ValueRO;

                var tb = OneBitRob.AI.UnitBrainRegistry.Get(dot.Target);

                // Maintain single persistent VFX for this Target+EffectId across all casters
                if (dot.EffectVfxIdHash != 0 && dot.Target != Entity.Null && tb != null)
                {
                    long salt = unchecked((long)0x517CC1B727220A95UL);
                    long tKey = ((long)dot.Target.Index << 32) | (long)(uint)dot.Target.Version;
                    long key = tKey ^ (((long)dot.EffectVfxIdHash) << 1) ^ salt;

                    bool hasBind = em.HasComponent<ActiveTargetVfx>(caster);
                    if (!hasBind)
                    {
                        VfxService.BeginPersistentByHash(dot.EffectVfxIdHash, key, tb.transform.position, tb.transform);
                        ecb.AddComponent(caster, new ActiveTargetVfx { Key = key, IdHash = dot.EffectVfxIdHash, Target = dot.Target });
                    }
                    else
                    {
                        var bind = em.GetComponentData<ActiveTargetVfx>(caster);
                        if (bind.Target != dot.Target || bind.IdHash != dot.EffectVfxIdHash)
                        {
                            VfxService.EndPersistent(bind.Key);

                            long newKey = tKey ^ (((long)dot.EffectVfxIdHash) << 1) ^ salt;
                            VfxService.BeginPersistentByHash(dot.EffectVfxIdHash, newKey, tb.transform.position, tb.transform);
                            bind.Key = newKey;
                            bind.IdHash = dot.EffectVfxIdHash;
                            bind.Target = dot.Target;
                            ecb.SetComponent(caster, bind);
                        }
                        else
                        {
                            VfxService.MovePersistent(bind.Key, bind.IdHash, tb.transform.position, tb.transform);
                        }
                    }
                }
                else
                {
                    if (em.HasComponent<ActiveTargetVfx>(caster))
                    {
                        var bind = em.GetComponentData<ActiveTargetVfx>(caster);
                        VfxService.EndPersistent(bind.Key);
                        ecb.RemoveComponent<ActiveTargetVfx>(caster);
                    }
                }

                // Tick gameplay effect
                dot.Remaining -= SystemAPI.Time.DeltaTime;
                bool finished = dot.Remaining <= 0f;

                if (!finished && dot.NextTick <= now)
                {
                    if (tb != null && tb.Health != null)
                    {
                        bool isHot = dot.Positive != 0;
                        float shownAmount = math.abs(dot.AmountPerTick);

                        float amt = isHot ? -shownAmount : shownAmount;
                        tb.Health.Damage(amt, tb.gameObject, 0f, 0f, Vector3.zero);

                        DamageNumbersManager.Popup(
                            new DamageNumbersParams
                            {
                                Kind = isHot ? DamagePopupKind.Hot : DamagePopupKind.Dot,
                                Follow = tb.transform,
                                Position = tb.transform.position,
                                Amount = shownAmount
                            }
                        );
                    }

                    dot.NextTick = now + math.max(0.05f, dot.Interval);
                }

                if (finished)
                {
                    if (em.HasComponent<ActiveTargetVfx>(caster))
                    {
                        var bind = em.GetComponentData<ActiveTargetVfx>(caster);
                        VfxService.EndPersistent(bind.Key);
                        ecb.RemoveComponent<ActiveTargetVfx>(caster);
                    }

                    ecb.RemoveComponent<DotOnTarget>(caster);
                }
                else { dotRW.ValueRW = dot; }
            }

            // Area DoT/HoT (persistent area VFX)
            foreach (var (area, e) in SystemAPI.Query<RefRW<DoTArea>>().WithEntityAccess())
            {
                var a = area.ValueRO;

                if (a.AreaVfxIdHash != 0)
                {
                    long salt  = unchecked((long)0x9E3779B97F4A7C15UL);
                    long keyLo = ((long)e.Index << 32) | (long)(uint)e.Version;
                    long key   = keyLo ^ (((long)a.AreaVfxIdHash) << 1) ^ salt;

                    var vfxPos = a.Position + new float3(0f, a.VfxYOffset, 0f);

                    if (!em.HasComponent<ActiveAreaVfx>(e))
                    {
                        VfxService.BeginPersistentByHash(a.AreaVfxIdHash, key, vfxPos, null);
                        ecb.AddComponent(e, new ActiveAreaVfx { Key = key, IdHash = a.AreaVfxIdHash });
                    }
                    else { VfxService.MovePersistent(key, a.AreaVfxIdHash, vfxPos, null); }
                }
                else if (em.HasComponent<ActiveAreaVfx>(e))
                {
                    var av = em.GetComponentData<ActiveAreaVfx>(e);
                    VfxService.EndPersistent(av.Key);
                    ecb.RemoveComponent<ActiveAreaVfx>(e);
                }

                // Lifetime
                a.Remaining -= SystemAPI.Time.DeltaTime;
                if (a.Remaining <= 0f)
                {
                    if (em.HasComponent<ActiveAreaVfx>(e))
                    {
                        var av = em.GetComponentData<ActiveAreaVfx>(e);
                        VfxService.EndPersistent(av.Key);
                        ecb.RemoveComponent<ActiveAreaVfx>(e);
                    }

                    ecb.RemoveComponent<DoTArea>(e);
                    continue;
                }

                // Ticks
                if (a.NextTick <= now)
                {
                    int count = Physics.OverlapSphereNonAlloc(
                        (Vector3)a.Position, a.Radius, s_Cols, a.LayerMask, QueryTriggerInteraction.Collide
                    );

                    bool isHot = a.Positive != 0;
                    float shownAmount = math.abs(a.AmountPerTick);

                    for (int i = 0; i < count; i++)
                    {
                        var col = s_Cols[i];
                        if (!col) continue;
                        var tb = col.GetComponentInParent<OneBitRob.AI.UnitBrain>();
                        if (tb == null || tb.Health == null) continue;

                        float amt = isHot ? -shownAmount : shownAmount;
                        tb.Health.Damage(amt, tb.gameObject, 0f, 0f, Vector3.zero);

                        DamageNumbersManager.Popup(
                            new DamageNumbersParams
                            {
                                Kind = isHot ? DamagePopupKind.Hot : DamagePopupKind.Dot,
                                Follow = tb.transform,
                                Position = tb.transform.position,
                                Amount = shownAmount
                            }
                        );
                    }

                    a.NextTick = now + math.max(0.05f, a.Interval);
                }

                area.ValueRW = a;
            }

            foreach (var (av, e) in SystemAPI.Query<RefRO<ActiveAreaVfx>>().WithNone<DoTArea>().WithEntityAccess())
            {
                VfxService.EndPersistent(av.ValueRO.Key);
                ecb.RemoveComponent<ActiveAreaVfx>(e);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}
