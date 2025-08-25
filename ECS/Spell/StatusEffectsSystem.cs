// FILE: Assets/PROJECT/Scripts/Runtime/ECS/StatusEffectsSystem.cs
//
// Key changes in DotOnTarget handling:
// - Compute a stable key from Target + EffectVfxIdHash
// - Begin persistent once (adds ActiveTargetVfx on caster), then Move each update
// - End persistent when effect finishes or target changes
// - Remove PlayByHash() per tick (that caused stacking); keep Damage Numbers
//
// AoE path: unchanged except it already uses persistent area VFX.

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

        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float now = (float)SystemAPI.Time.ElapsedTime;

            // ───────── Target DoT/HoT (now persistent visual per Target+EffectId)
            foreach (var (dotRW, caster) in SystemAPI.Query<RefRW<DotOnTarget>>().WithEntityAccess())
            {
                var dot = dotRW.ValueRO;

                // Resolve target UnitBrain for visuals/damage numbers
                var tb = OneBitRob.AI.UnitBrainRegistry.Get(dot.Target);

                // 1) Maintain a SINGLE persistent VFX for this Target+EffectId across ALL casters
                //    Key = (target index/version) XOR vfx id hash (salted)
                if (dot.EffectVfxIdHash != 0 && dot.Target != Entity.Null && tb != null)
                {
                    long salt = unchecked((long)0x517CC1B727220A95UL);
                    long tKey = ((long)dot.Target.Index << 32) | (long)(uint)dot.Target.Version;
                    long key = tKey ^ (((long)dot.EffectVfxIdHash) << 1) ^ salt;

                    // Check if this caster already bound to this target visual
                    bool hasBind = em.HasComponent<ActiveTargetVfx>(caster);
                    if (!hasBind)
                    {
                        // First time for this caster: Begin (refCount++)
                        OneBitRob.FX.SpellVfxPoolManager.BeginPersistentByHash(dot.EffectVfxIdHash, key, tb.transform.position, tb.transform);
                        ecb.AddComponent(caster, new ActiveTargetVfx { Key = key, IdHash = dot.EffectVfxIdHash, Target = dot.Target });
                    }
                    else
                    {
                        var bind = em.GetComponentData<ActiveTargetVfx>(caster);

                        // If the target or vfx id changed, release the previous and re-bind
                        if (bind.Target != dot.Target || bind.IdHash != dot.EffectVfxIdHash)
                        {
                            OneBitRob.FX.SpellVfxPoolManager.EndPersistent(bind.Key);

                            long newKey = tKey ^ (((long)dot.EffectVfxIdHash) << 1) ^ salt;
                            OneBitRob.FX.SpellVfxPoolManager.BeginPersistentByHash(dot.EffectVfxIdHash, newKey, tb.transform.position, tb.transform);
                            bind.Key = newKey;
                            bind.IdHash = dot.EffectVfxIdHash;
                            bind.Target = dot.Target;
                            ecb.SetComponent(caster, bind);
                        }
                        else
                        {
                            // Normal path: keep it attached & alive (handles rare auto-despawn)
                            OneBitRob.FX.SpellVfxPoolManager.MovePersistent(bind.Key, bind.IdHash, tb.transform.position, tb.transform);
                        }
                    }
                }
                else
                {
                    // No VFX id or target lost: if this caster had an active bind, release it
                    if (em.HasComponent<ActiveTargetVfx>(caster))
                    {
                        var bind = em.GetComponentData<ActiveTargetVfx>(caster);
                        OneBitRob.FX.SpellVfxPoolManager.EndPersistent(bind.Key);
                        ecb.RemoveComponent<ActiveTargetVfx>(caster);
                    }
                }

                // 2) Tick gameplay effect
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

                        OneBitRob.FX.DamageNumbersManager.Popup(
                            new OneBitRob.FX.DamageNumbersParams
                            {
                                Kind = isHot ? OneBitRob.FX.DamagePopupKind.Hot : OneBitRob.FX.DamagePopupKind.Dot,
                                Follow = tb.transform,
                                Position = tb.transform.position,
                                Amount = shownAmount
                            }
                        );
                    }

                    dot.NextTick = now + math.max(0.05f, dot.Interval);
                }

                // 3) Cleanup on finish
                if (finished)
                {
                    if (em.HasComponent<ActiveTargetVfx>(caster))
                    {
                        var bind = em.GetComponentData<ActiveTargetVfx>(caster);
                        OneBitRob.FX.SpellVfxPoolManager.EndPersistent(bind.Key);
                        ecb.RemoveComponent<ActiveTargetVfx>(caster);
                    }

                    ecb.RemoveComponent<DotOnTarget>(caster);
                }
                else { dotRW.ValueRW = dot; }
            }

            // ───────── Area DoT/HoT (persistent) — unchanged except MovePersistent now passes idHash
            foreach (var (area, e) in SystemAPI.Query<RefRW<DoTArea>>().WithEntityAccess())
            {
                var a = area.ValueRO;

                // Persistent VFX — use visual offset on Y only
                if (a.AreaVfxIdHash != 0)
                {
                    long salt = unchecked((long)0x9E3779B97F4A7C15UL);
                    long keyLo = ((long)e.Index << 32) | (long)(uint)e.Version;
                    long key = keyLo ^ (((long)a.AreaVfxIdHash) << 1) ^ salt;

                    var vfxPos = a.Position + new float3(0f, a.VfxYOffset, 0f);

                    if (!em.HasComponent<ActiveAreaVfx>(e))
                    {
                        OneBitRob.FX.SpellVfxPoolManager.BeginPersistentByHash(a.AreaVfxIdHash, key, (UnityEngine.Vector3)vfxPos, null);
                        ecb.AddComponent(e, new ActiveAreaVfx { Key = key, IdHash = a.AreaVfxIdHash });
                    }
                    else { OneBitRob.FX.SpellVfxPoolManager.MovePersistent(key, a.AreaVfxIdHash, (UnityEngine.Vector3)vfxPos, null); }
                }
                else if (em.HasComponent<ActiveAreaVfx>(e))
                {
                    var av = em.GetComponentData<ActiveAreaVfx>(e);
                    OneBitRob.FX.SpellVfxPoolManager.EndPersistent(av.Key);
                    ecb.RemoveComponent<ActiveAreaVfx>(e);
                }

                // Lifetime
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

                // Ticks (damage sampling at ground center)
                if (a.NextTick <= now)
                {
                    int count = UnityEngine.Physics.OverlapSphereNonAlloc(
                        (UnityEngine.Vector3)a.Position, a.Radius, s_Cols, a.LayerMask, UnityEngine.QueryTriggerInteraction.Collide
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
                        tb.Health.Damage(amt, tb.gameObject, 0f, 0f, UnityEngine.Vector3.zero);

                        OneBitRob.FX.DamageNumbersManager.Popup(
                            new OneBitRob.FX.DamageNumbersParams
                            {
                                Kind = isHot ? OneBitRob.FX.DamagePopupKind.Hot : OneBitRob.FX.DamagePopupKind.Dot,
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

            // Orphan area FX cleanup (unchanged)
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