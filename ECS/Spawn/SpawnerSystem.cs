// FILE: OneBitRob/ECS/SpawnerSystem.cs

using System.Collections.Generic;
using GPUInstancerPro.PrefabModule;
using OneBitRob.ECS.GPUI;
using Opsive.BehaviorDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;

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
            var deltaTime = SystemAPI.Time.DeltaTime;
            var data = SystemAPI.ManagedAPI.GetSingleton<SpawnerData>();
            var timerEnt = SystemAPI.GetSingletonEntity<SpawnerTimer>();
            var timer = SystemAPI.GetComponent<SpawnerTimer>(timerEnt);

            timer.ElapsedTime += deltaTime;
            if (timer.ElapsedTime < data.SpawnFrequency)
            {
                SystemAPI.SetComponent(timerEnt, timer);
                return;
            }

            timer.ElapsedTime = 0f;
            SystemAPI.SetComponent(timerEnt, timer);

            // Find SpawnMarkers
            List<Vector3> allySpawnCenter = new();
            List<Vector3> enemySpawnCenter = new();

            foreach (var (marker, ltw) in SystemAPI.Query<RefRO<SpawnerMarker>, RefRO<LocalTransform>>())
            {
                switch (marker.ValueRO.Type)
                {
                    case SpawnerType.Ally:
                        allySpawnCenter.Add(ltw.ValueRO.Position);
                        break;
                    case SpawnerType.Enemy:
                        enemySpawnCenter.Add(ltw.ValueRO.Position);
                        break;
                }
            }

            foreach (var pos in allySpawnCenter)
                SpawnGroup(ref state, data.EntityPrefab, data.AllyPrefabs, data.UnitsSpawnCount, pos, data, Constants.GameConstants.ALLY_FACTION);

            foreach (var pos in enemySpawnCenter)
                SpawnGroup(ref state, data.EntityPrefab, data.EnemyPrefabs, data.UnitsSpawnCount, pos, data, Constants.GameConstants.ENEMY_FACTION);

            EnigmaLogger.Log("Spawn Round Completed. Enabling Behaviour Trees", "INFO");
            BehaviorTree.EnableBakedBehaviorTreeSystem(World.DefaultGameObjectInjectionWorld);
        }

        private void SpawnGroup(ref SystemState state, Entity behaviorTreeEntityPrefab, GameObject[] unitPrefabs, int count, Vector3 spawnCenter, SpawnerData data, byte faction)
        {
            if (unitPrefabs == null || unitPrefabs.Length == 0) return;

            int totalUnits = count * unitPrefabs.Length;
            var brains = state.EntityManager.Instantiate(behaviorTreeEntityPrefab, totalUnits, Allocator.Temp);
            int entityIndex = 0;
            var gpuiManager = SystemAPI.ManagedAPI.GetSingleton<GPUIManagerRef>().Value;

            for (int prefabIndex = 0; prefabIndex < unitPrefabs.Length; ++prefabIndex)
            {
                var bodyPrefab = unitPrefabs[prefabIndex];

                for (int i = 0; i < count; ++i)
                {
                    var e = brains[entityIndex++];

                    var rand = Unity.Mathematics.Random.CreateFromIndex((uint)Time.frameCount + (uint)e.Index);
                    var pos = GetRandomPositionInArea(data.SpawnAreaFrom, data.SpawnAreaTo, rand) + spawnCenter;
                    state.EntityManager.SetComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));

                    // Visual (GPUI only)
                    var go = Object.Instantiate(bodyPrefab, pos, Quaternion.identity);
                    var gpui = go.GetComponent<GPUIPrefab>();
                    if (!gpui)
                    {
                        Debug.LogError($"GPUIPrefab missing on {bodyPrefab.name}");
                        continue;
                    }

                    GPUIPrefabAPI.AddPrefabInstanceImmediate(gpuiManager, gpui);

                    var unitBrainMono = go.GetComponent<AI.UnitBrain>();
                    unitBrainMono.SetEntity(e); // registers in UnitBrainRegistry

                    // ─────────────────────────────────────────────────────────────
                    // Baseline tags & targeting membership (must exist immediately)
                    state.EntityManager.AddComponent(e, ComponentType.ReadOnly<AgentTag>());
                    state.EntityManager.AddComponentData(e, new SpatialHashComponents.SpatialHashTarget { Faction = faction });

                    if (faction == Constants.GameConstants.ALLY_FACTION)
                        state.EntityManager.AddComponent<AllyTag>(e);
                    else if (faction == Constants.GameConstants.ENEMY_FACTION)
                        state.EntityManager.AddComponent<EnemyTag>(e);

                    // Baseline AI shell (read by systems even before Setup runs)
                    state.EntityManager.AddComponentData(e, new Target { Value = Entity.Null });
                    state.EntityManager.AddComponentData(e, new DesiredDestination { Position = float3.zero, HasValue = 0 });
                    state.EntityManager.AddComponentData(e, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });

                    state.EntityManager.AddComponentData(e, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
                    state.EntityManager.AddComponentData(e, new Alive { Value = 1 });

                    // Attack shell present at spawn
                    state.EntityManager.AddComponentData(e, new AttackRequest { Target = Entity.Null, HasValue = 0 });
                    state.EntityManager.AddComponentData(e, new AttackCooldown { NextTime = 0f });
                    state.EntityManager.AddComponentData(e, new AttackWindup { Active = 0, ReleaseTime = 0f });

                    // Spell shell present at spawn (full SpellConfig comes in Setup)
                    state.EntityManager.AddComponentData(e, new SpellState { CanCast = 1, Ready = 1 });
                    state.EntityManager.AddComponentData(e, new CastRequest { Kind = CastKind.None, Target = Entity.Null, AoEPosition = float3.zero, HasValue = 0 });

                    // NOTE:
                    // - HealthMirror, CombatStyle, SpellConfig, Spell* runtime, RetargetAssist/Cooldown
                    //   are now initialized in AI/SetupUnitSystem after the brain is registered.
                }
            }
        }

        private Vector3 GetRandomPositionInArea(Vector3 from, Vector3 to, Unity.Mathematics.Random random)
        {
            return new Vector3(
                random.NextFloat(min(from.x, to.x), max(from.x, to.x)),
                random.NextFloat(min(from.y, to.y), max(from.y, to.y)),
                random.NextFloat(min(from.z, to.z), max(from.z, to.z))
            );
        }
    }
}