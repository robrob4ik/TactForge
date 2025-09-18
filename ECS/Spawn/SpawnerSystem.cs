// File: OneBitRob/ECS/SpawnerSystem.cs
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

using GPUInstancerPro.PrefabModule;
using OneBitRob.ECS.GPUI;
using Opsive.BehaviorDesigner.Runtime;
using Unity.Transforms;

using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;

namespace OneBitRob.ECS
{
    public partial struct SpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnerData>();
            state.RequireForUpdate<SpawnerTimer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var data      = SystemAPI.ManagedAPI.GetSingleton<SpawnerData>();

            var timerEnt  = SystemAPI.GetSingletonEntity<SpawnerTimer>();
            var timer     = SystemAPI.GetComponent<SpawnerTimer>(timerEnt);

            timer.ElapsedTime += deltaTime;
            if (timer.ElapsedTime < data.SpawnFrequency)
            {
                SystemAPI.SetComponent(timerEnt, timer);
                return;
            }

            timer.ElapsedTime = 0f;
            SystemAPI.SetComponent(timerEnt, timer);

            // Gather spawn anchors
            var allySpawnCenter  = new List<Vector3>();
            var enemySpawnCenter = new List<Vector3>();
            foreach (var (marker, ltw) in SystemAPI.Query<RefRO<SpawnerMarker>, RefRO<LocalTransform>>())
            {
                switch (marker.ValueRO.Type)
                {
                    case SpawnerType.Ally:  allySpawnCenter.Add(ltw.ValueRO.Position); break;
                    case SpawnerType.Enemy: enemySpawnCenter.Add(ltw.ValueRO.Position); break;
                }
            }

            // Try to get the GPUI Prefab Manager (may be null -> fallback path handles it)
            GPUIPrefabManager gpuiMgr = null;
            try
            {
                gpuiMgr = SystemAPI.ManagedAPI.GetSingleton<GPUIManagerRef>()?.Value;
            }
            catch { /* No GPUI manager singleton - we will fallback to manager-agnostic add. */ }

            foreach (var pos in allySpawnCenter)
                SpawnGroup(ref state, data, data.AllyPrefabs, pos, Constants.GameConstants.ALLY_FACTION, gpuiMgr);

            foreach (var pos in enemySpawnCenter)
                SpawnGroup(ref state, data, data.EnemyPrefabs, pos, Constants.GameConstants.ENEMY_FACTION, gpuiMgr);

            EnigmaLogger.Log("Spawn Round Completed. Enabling Behaviour Trees", "INFO");
            BehaviorTree.EnableBakedBehaviorTreeSystem(World.DefaultGameObjectInjectionWorld);
        }

        private void SpawnGroup(
            ref SystemState state,
            SpawnerData data,
            GameObject[] unitPrefabs,
            Vector3 spawnCenter,
            byte faction,
            GPUIPrefabManager gpuiManager)
        {
            if (unitPrefabs == null || unitPrefabs.Length == 0) return;

            int countPerPrefab = data.UnitsSpawnCount;
            int totalUnits     = countPerPrefab * unitPrefabs.Length;

            var brains = state.EntityManager.Instantiate(data.EntityPrefab, totalUnits, Allocator.Temp);
            int entityIndex = 0;

            bool anyImmediateAdds = false;

            for (int prefabIndex = 0; prefabIndex < unitPrefabs.Length; ++prefabIndex)
            {
                var bodyPrefab = unitPrefabs[prefabIndex];

                // Make sure the manager knows this prefab as a prototype (safe to call even if already added)
                int protoIndexOnMgr = -1;
                if (gpuiManager != null)
                {
                    protoIndexOnMgr = gpuiManager.GetPrototypeIndex(bodyPrefab);
                    if (protoIndexOnMgr < 0)
                        protoIndexOnMgr = GPUIPrefabAPI.AddPrototype(gpuiManager, bodyPrefab); // runtime define
                }

                for (int i = 0; i < countPerPrefab; ++i)
                {
                    var e = brains[entityIndex++];

                    var rand = Unity.Mathematics.Random.CreateFromIndex((uint)Time.frameCount + (uint)e.Index);
                    var pos  = GetRandomPositionInArea(data.SpawnAreaFrom, data.SpawnAreaTo, rand) + spawnCenter;

                    state.EntityManager.SetComponentData(
                        e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));

                    // Instantiate, wire brain, and register with GPUI
                    var go = UnityEngine.Object.Instantiate(bodyPrefab, pos, Quaternion.identity);

                    // Bridge Mono brain to ECS entity
                    var unitBrainMono = go.GetComponent<AI.UnitBrain>();
                    unitBrainMono.SetEntity(e);

                    // Register with GPUI
                    var gpui = go.GetComponent<GPUIPrefab>();
                    if (gpui != null)
                    {
                        bool usedImmediate = false;

                        if (gpuiManager != null && protoIndexOnMgr >= 0)
                        {
                            // Immediate add -> shows up this frame, disables Unity renderers
                            int addRes = GPUIPrefabAPI.AddPrefabInstanceImmediate(gpuiManager, gpui, protoIndexOnMgr);
                            usedImmediate = addRes >= 0;
                        }

                        if (!usedImmediate)
                        {
                            // Manager-agnostic queued add (registers on next LateUpdate)
                            GPUIPrefabAPI.AddPrefabInstance(gpui);
                        }

                        anyImmediateAdds |= usedImmediate;
                    }
                    else
                    {
                        // If you reach here, prefab wasn't added to a Prefab Manager as a prototype
                        // (GPUI won't have attached the GPUIPrefab component to the asset).
                        // Either add it in the Prefab Manager or keep the runtime AddPrototype above.
                        // EnigmaLogger.Log($"GPUI: Prefab '{bodyPrefab.name}' has no GPUIPrefab component.", "WARN");
                    }

                    PrimeAgentEntity(ref state, e, faction);
                }
            }

            // If we immediately added instances and Transform Updates are disabled on the prototype,
            // push transforms once so they render in the right place without waiting.
            if (anyImmediateAdds && gpuiManager != null)
            {
                GPUIPrefabAPI.UpdateTransformData(gpuiManager);
            }
        }

        private void PrimeAgentEntity(ref SystemState state, Entity e, byte faction)
        {
            var em = state.EntityManager;

            // Baseline tags & targeting membership
            em.AddComponent(e, ComponentType.ReadOnly<AgentTag>());
            em.AddComponentData(e, new SpatialHashTarget { Faction = faction });

            if (faction == Constants.GameConstants.ALLY_FACTION) em.AddComponent<AllyTag>(e);
            else if (faction == Constants.GameConstants.ENEMY_FACTION) em.AddComponent<EnemyTag>(e);

            // Baseline AI shell
            em.AddComponentData(e, new Target { Value = Entity.Null });
            em.AddComponentData(e, new DesiredDestination { Position = float3.zero, HasValue = 0 });
            em.AddComponentData(e, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });
            em.AddComponentData(e, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
            em.AddComponentData(e, new Alive { Value = 1 });

            // Attack shell
            em.AddComponentData(e, new AttackCooldown { NextTime = 0f });
            em.AddComponentData(e, new AttackWindup { Active = 0, ReleaseTime = 0f });
            if (!em.HasComponent<AttackRequest>(e)) { em.AddComponent<AttackRequest>(e); em.SetComponentEnabled<AttackRequest>(e, false); }

            // Spell shell (add disabled by default)
            if (!em.HasComponent<CastRequest>(e))                 { em.AddComponent<CastRequest>(e);                 em.SetComponentEnabled<CastRequest>(e, false); }
            if (!em.HasComponent<SpellProjectileSpawnRequest>(e)) { em.AddComponent<SpellProjectileSpawnRequest>(e); em.SetComponentEnabled<SpellProjectileSpawnRequest>(e, false); }
            if (!em.HasComponent<EcsProjectileSpawnRequest>(e))   { em.AddComponent<EcsProjectileSpawnRequest>(e);   em.SetComponentEnabled<EcsProjectileSpawnRequest>(e, false); }
            if (!em.HasComponent<MeleeHitRequest>(e))             { em.AddComponent<MeleeHitRequest>(e);             em.SetComponentEnabled<MeleeHitRequest>(e, false); }

            // Spell state shell
            if (!em.HasComponent<SpellState>(e))    em.AddComponentData(e, new SpellState { CanCast = 1, Ready = 1 });
            if (!em.HasComponent<SpellCooldown>(e)) em.AddComponentData(e, new SpellCooldown { NextTime = 0f });
            if (!em.HasComponent<SpellWindup>(e))   em.AddComponentData(e, new SpellWindup { Active = 0, ReleaseTime = 0f });
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
