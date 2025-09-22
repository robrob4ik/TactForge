using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.ECS.GPUI;
using OneBitRob.VFX;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Reflection;
using OneBitRob.Core;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    public partial struct Brain_EcsToMono_SummonBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (reqRW, caster) in SystemAPI.Query<RefRW<SummonRequest>>().WithEntityAccess())
            {
                // enableable pattern replaces HasValue
                if (!SystemAPI.IsComponentEnabled<SummonRequest>(caster))
                    continue;

                var req = reqRW.ValueRO;

                var prefab = VisualAssetRegistry.GetSummonPrefab(req.PrefabIdHash);
                if (prefab != null
                    && SystemAPI.ManagedAPI.TryGetSingleton<SpawnerData>(out var data)
                    && SystemAPI.ManagedAPI.TryGetSingleton<GPUIManagerRef>(out var gpuiRef))
                {
                    var gpuiMgr = gpuiRef.Value;
                    int count   = math.max(1, req.Count);

                    for (int i = 0; i < count; i++)
                    {
                        // 1) Create ECS brain entity
                        var brainEnt = em.Instantiate(data.EntityPrefab);

                        var pos = req.Position + new float3(
                            UnityEngine.Random.Range(-0.5f, 0.5f), 0f,
                            UnityEngine.Random.Range(-0.5f, 0.5f)
                        );
                        em.SetComponentData(brainEnt, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));

                        // 2) Instantiate visual & register to GPUI safely
                        var go   = Object.Instantiate(prefab, (Vector3)pos, Quaternion.identity);
                        var gpui = go.GetComponent<GPUIPrefab>();
                        TryRegisterGPUIInstance(gpuiMgr, gpui);

                        // 3) Link Mono brain ↔ ECS entity
                        var monoBrain = go.GetComponent<UnitBrain>();
                        monoBrain?.SetEntity(brainEnt);

                        UnitStaticSetup.Apply(em, brainEnt, monoBrain);

                        // 4) Baseline ECS shell (mirrors SpawnerSystem)
                        em.AddComponent(brainEnt, ComponentType.ReadOnly<AgentTag>());
                        em.AddComponentData(brainEnt, new SpatialHashTarget { Faction = req.Faction });

                        if (req.Faction == Constants.GameConstants.ALLY_FACTION) em.AddComponent<AllyTag>(brainEnt);
                        else if (req.Faction == Constants.GameConstants.ENEMY_FACTION) em.AddComponent<EnemyTag>(brainEnt);

                        em.AddComponentData(brainEnt, new Target { Value = Entity.Null });
                        em.AddComponentData(brainEnt, new DesiredDestination { Position = float3.zero, HasValue = 0 });
                        em.AddComponentData(brainEnt, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });
                        em.AddComponentData(brainEnt, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
                        em.AddComponentData(brainEnt, new Alive { Value = 1 });

                        // Attack shell + enableables
                        em.AddComponentData(brainEnt, new AttackCooldown { NextTime = 0f });
                        em.AddComponentData(brainEnt, new AttackWindup   { Active = 0, ReleaseTime = 0f });

                        if (!em.HasComponent<AttackRequest>(brainEnt)) { em.AddComponent<AttackRequest>(brainEnt); em.SetComponentEnabled<AttackRequest>(brainEnt, false); }
                        if (!em.HasComponent<EcsProjectileSpawnRequest>(brainEnt)) { em.AddComponent<EcsProjectileSpawnRequest>(brainEnt); em.SetComponentEnabled<EcsProjectileSpawnRequest>(brainEnt, false); }
                        if (!em.HasComponent<MeleeHitRequest>(brainEnt)) { em.AddComponent<MeleeHitRequest>(brainEnt); em.SetComponentEnabled<MeleeHitRequest>(brainEnt, false); }

                        // Spell shell + enableables
                        if (!em.HasComponent<CastRequest>(brainEnt)) { em.AddComponent<CastRequest>(brainEnt); em.SetComponentEnabled<CastRequest>(brainEnt, false); }
                        if (!em.HasComponent<SpellProjectileSpawnRequest>(brainEnt)) { em.AddComponent<SpellProjectileSpawnRequest>(brainEnt); em.SetComponentEnabled<SpellProjectileSpawnRequest>(brainEnt, false); }

                        if (!em.HasComponent<SpellState>(brainEnt))   em.AddComponentData(brainEnt, new SpellState { CanCast = 1, Ready = 1 });
                        if (!em.HasComponent<SpellCooldown>(brainEnt)) em.AddComponentData(brainEnt, new SpellCooldown { NextTime = 0f });
                        if (!em.HasComponent<SpellWindup>(brainEnt))   em.AddComponentData(brainEnt, new SpellWindup { Active = 0, ReleaseTime = 0f });

                        // Health & style from definition (if any)
                        int hp = monoBrain && monoBrain.UnitDefinition ? monoBrain.UnitDefinition.health : 100;
                        ecb.SetOrAdd(em, brainEnt, new HealthMirror { Current = hp, Max = hp });

                        byte style = 1;
                        var weapon = monoBrain && monoBrain.UnitDefinition ? monoBrain.UnitDefinition.weapon : null;
                        if (weapon is RangedWeaponDefinition) style = 2;
                        ecb.SetOrAdd(em, brainEnt, new CombatStyle { Value = style });

                        // Retarget helpers
                        if (!em.HasComponent<RetargetCooldown>(brainEnt))
                            ecb.AddComponent(brainEnt, new RetargetCooldown { NextTime = 0 });

                        ecb.SetOrAdd(em, brainEnt, new RetargetAssist
                        {
                            LastPos       = pos,
                            LastDistSq    = float.MaxValue,
                            NoProgressTime= 0f
                        });
                    }
                }

                // consume request via enableable flag
                SystemAPI.SetComponentEnabled<SummonRequest>(caster, false);
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void TryRegisterGPUIInstance(GPUIPrefabManager manager, GPUIPrefab prefab)
        {
            if (manager == null || prefab == null) return;

            var mgrType = manager.GetType();
            var pfType  = prefab.GetType();

            // Try (GPUIPrefab) signatures
            foreach (var name in new[] { "AddPrefabInstanceImmediate", "AddPrefabInstance", "RegisterPrefabInstance" })
            {
                var m = mgrType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                          null, new[] { pfType }, null);
                if (m != null) { m.Invoke(manager, new object[] { prefab }); return; }
            }

            // Try (GameObject) signatures
            foreach (var name in new[] { "AddPrefabInstanceImmediate", "AddPrefabInstance", "RegisterPrefabInstance" })
            {
                var m = mgrType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                          null, new[] { typeof(GameObject) }, null);
                if (m != null) { m.Invoke(manager, new object[] { prefab.gameObject }); return; }
            }
        }
    }
}
