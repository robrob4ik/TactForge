using System.Collections.Generic;
using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.Constants;
using OneBitRob.ECS.GPUI;
using Opsive.BehaviorDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.ECS
{
    public partial struct SpawnerSystem : ISystem
    {
        private void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnerData>();
            state.RequireForUpdate<SpawnerTimer>();
        }

        private void OnUpdate(ref SystemState state)
        {
            var deltaTime  = SystemAPI.Time.DeltaTime;
            var data       = SystemAPI.ManagedAPI.GetSingleton<SpawnerData>();
            var timerEnt   = SystemAPI.GetSingletonEntity<SpawnerTimer>();
            var timer      = SystemAPI.GetComponent<SpawnerTimer>(timerEnt);

            timer.ElapsedTime += deltaTime;
            if (timer.ElapsedTime < data.SpawnFrequency)
            {
                SystemAPI.SetComponent(timerEnt, timer);
                return;
            }

            timer.ElapsedTime = 0f;
            SystemAPI.SetComponent(timerEnt, timer);

            // Find SpawnMarkers
            List<Vector3> allySpawnCenter  = new();
            List<Vector3> enemySpawnCenter = new();

            foreach (var (marker, ltw) in SystemAPI.Query<RefRO<SpawnerMarker>, RefRO<LocalTransform>>())
            {
                switch (marker.ValueRO.Type)
                {
                    case SpawnerType.Ally:  allySpawnCenter.Add(ltw.ValueRO.Position);  break;
                    case SpawnerType.Enemy: enemySpawnCenter.Add(ltw.ValueRO.Position); break;
                }
            }

            // Spawn
            foreach (var pos in allySpawnCenter)
                SpawnGroup(ref state, data.EntityPrefab, data.AllyPrefabs, data.UnitsSpawnCount, pos, data, GameConstants.ALLY_FACTION);

            foreach (var pos in enemySpawnCenter)
                SpawnGroup(ref state, data.EntityPrefab, data.EnemyPrefabs, data.UnitsSpawnCount, pos, data, GameConstants.ENEMY_FACTION);

            EnigmaLogger.Log("Spawn Round Completed. Enabling Behaviour Trees", "INFO");
            BehaviorTree.EnableBakedBehaviorTreeSystem(World.DefaultGameObjectInjectionWorld);
        }

        private void SpawnGroup(ref SystemState state, Entity behaviorTreeEntityPrefab, GameObject[] unitPrefabs, int count, Vector3 spawnCenter, SpawnerData data, byte faction)
        {
            if (unitPrefabs == null || unitPrefabs.Length == 0) return;

            int totalUnits = count * unitPrefabs.Length;
            var brains     = state.EntityManager.Instantiate(behaviorTreeEntityPrefab, totalUnits, Allocator.Temp);
            int entityIndex = 0;
            var gpuiManager = SystemAPI.ManagedAPI.GetSingleton<GPUIManagerRef>().Value;

            for (int prefabIndex = 0; prefabIndex < unitPrefabs.Length; ++prefabIndex)
            {
                var bodyPrefab = unitPrefabs[prefabIndex];

                for (int i = 0; i < count; ++i)
                {
                    var e = brains[entityIndex++];

                    var rand = Unity.Mathematics.Random.CreateFromIndex((uint)Time.frameCount + (uint)e.Index);
                    var pos  = GetRandomPositionInArea(data.SpawnAreaFrom, data.SpawnAreaTo, rand) + spawnCenter;
                    state.EntityManager.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));

                    /* ── 1) Visual (GPUI only) ───────────────────────────────── */
                    var go   = Object.Instantiate(bodyPrefab, pos, Quaternion.identity);
                    var gpui = go.GetComponent<GPUIPrefab>();

                    if (!gpui)
                    {
                        Debug.LogError($"GPUIPrefab missing on {bodyPrefab.name}");
                       // Object.Destroy(go);
                        continue;
                    }

                    GPUIPrefabAPI.AddPrefabInstanceImmediate(gpuiManager, gpui);

                    // 2) gameplay bindings (NO shared managed component)
                    var unitBrainMono = go.GetComponent<UnitBrain>();
                    unitBrainMono.SetEntity(e); // registers in UnitBrainRegistry

                    state.EntityManager.AddComponent(e, ComponentType.ReadOnly<AgentTag>());
                    state.EntityManager.AddComponentData(e, new SpatialHashComponents.SpatialHashTarget { Faction = faction });

                    if (faction == GameConstants.ALLY_FACTION)
                        state.EntityManager.AddComponent<AllyTag>(e);
                    else if (faction == GameConstants.ENEMY_FACTION)
                        state.EntityManager.AddComponent<EnemyTag>(e);
                    
                    state.EntityManager.AddComponentData(e, new Target { Value = Entity.Null });

                    state.EntityManager.AddComponentData(e, new DesiredDestination
                    {
                        Position = float3.zero,
                        HasValue = 0
                    });

                    state.EntityManager.AddComponentData(e, new DesiredFacing
                    {
                        TargetPosition = float3.zero,
                        HasValue = 0
                    });

                    state.EntityManager.AddComponentData(e, new AttackRequest
                    {
                        Target   = Entity.Null,
                        HasValue = 0
                    });

                    state.EntityManager.AddComponentData(e, new CastRequest
                    {
                        Kind     = CastKind.None,
                        Target   = Entity.Null,
                        AoEPosition = float3.zero,
                        HasValue = 0
                    });
                    
                    state.EntityManager.AddComponentData(e, new RetargetCooldown { NextTime = 0 });
                }
            }
        }

        private Vector3 GetRandomPositionInArea(Vector3 from, Vector3 to, Unity.Mathematics.Random random)
        {
            return new Vector3(
                random.NextFloat(math.min(from.x, to.x), math.max(from.x, to.x)),
                random.NextFloat(math.min(from.y, to.y), math.max(from.y, to.y)),
                random.NextFloat(math.min(from.z, to.z), math.max(from.z, to.z))
            );
        }
    }
}
