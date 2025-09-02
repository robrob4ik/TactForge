using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.ECS.GPUI;
using OneBitRob.VFX;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(EcsToMonoBridgeGroup))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial struct Brain_EcsToMono_SummonBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (req, caster) in SystemAPI.Query<RefRW<SummonRequest>>().WithEntityAccess())
            {
                if (req.ValueRO.HasValue == 0) continue;

                var prefab = VisualAssetRegistry.GetSummonPrefab(req.ValueRO.PrefabIdHash);
                if (prefab != null &&
                    SystemAPI.ManagedAPI.TryGetSingleton<SpawnerData>(out var data) &&
                    SystemAPI.ManagedAPI.TryGetSingleton<GPUIManagerRef>(out var gpuiRef))
                {
                    var gpuiMgr = gpuiRef.Value;
                    int count = math.max(1, req.ValueRO.Count);

                    for (int i = 0; i < count; i++)
                    {
                        var brainEnt = em.Instantiate(data.EntityPrefab);
                        var pos = req.ValueRO.Position + new float3(UnityEngine.Random.Range(-0.5f, 0.5f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f));

                        em.SetComponentData(brainEnt, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));

                        // Visual (Mono)
                        var go = Object.Instantiate(prefab, (Vector3)pos, Quaternion.identity);
                        var gpui = go.GetComponent<GPUIPrefab>();
                        if (gpui) GPUIPrefabAPI.AddPrefabInstanceImmediate(gpuiMgr, gpui);

                        var monoBrain = go.GetComponent<UnitBrain>();
                        monoBrain?.SetEntity(brainEnt);

                        // ECS tags & baseline components
                        em.AddComponent(brainEnt, ComponentType.ReadOnly<AgentTag>());
                        em.AddComponentData(brainEnt, new SpatialHashTarget { Faction = req.ValueRO.Faction });

                        if (req.ValueRO.Faction == Constants.GameConstants.ALLY_FACTION)  em.AddComponent<AllyTag>(brainEnt);
                        else if (req.ValueRO.Faction == Constants.GameConstants.ENEMY_FACTION) em.AddComponent<EnemyTag>(brainEnt);

                        em.AddComponentData(brainEnt, new Target { Value = Entity.Null });
                        em.AddComponentData(brainEnt, new DesiredDestination { Position = pos, HasValue = 0 });
                        em.AddComponentData(brainEnt, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });
                        em.AddComponentData(brainEnt, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
                        em.AddComponentData(brainEnt, new Alive { Value = 1 });

                        // Health & style (from Mono definition if present)
                        int hp = monoBrain && monoBrain.UnitDefinition ? monoBrain.UnitDefinition.health : 100;
                        em.AddComponentData(brainEnt, new HealthMirror { Current = hp, Max = hp });

                        byte style = 1;
                        var weapon = monoBrain && monoBrain.UnitDefinition ? monoBrain.UnitDefinition.weapon : null;
                        if (weapon is RangedWeaponDefinition) style = 2;
                        em.AddComponentData(brainEnt, new CombatStyle { Value = style });

                        // Basic combat state
                        em.AddComponentData(brainEnt, new SpellState { CanCast = 1, Ready = 1 });
                        em.AddComponentData(brainEnt, new AttackRequest { Target = Entity.Null, HasValue = 0 });
                        em.AddComponentData(brainEnt, new AttackCooldown { NextTime = 0f });
                        em.AddComponentData(brainEnt, new AttackWindup { Active = 0, ReleaseTime = 0f });
                        em.AddComponentData(brainEnt, new CastRequest { Kind = CastKind.None, Target = Entity.Null, AoEPosition = float3.zero, HasValue = 0 });

                        // Retarget helpers (guarded)
                        if (!em.HasComponent<RetargetCooldown>(brainEnt))
                            em.AddComponentData(brainEnt, new RetargetCooldown { NextTime = 0 });

                        em.AddComponentData(brainEnt, new RetargetAssist
                        {
                            LastPos = pos,
                            LastDistSq = float.MaxValue,
                            NoProgressTime = 0f
                        });
                    }
                }

                req.ValueRW = default;
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}
