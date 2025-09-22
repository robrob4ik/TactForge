using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.ECS.GPUI;
using OneBitRob.OneBitRob.Spawning;
// using OneBitRob.Spawning; // (only if you actually reference types from this ns)
using Unity.Collections;

using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MonoToEcsSyncGroup))]
    public partial struct EnemyWaveSpawnerSystem : ISystem
    {
        private EntityQuery _markerQ; // OK to store

        public void OnCreate(ref SystemState state)
        {
            _markerQ = state.GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<SpawnerMarker>(), ComponentType.ReadOnly<LocalTransform>() }
                }
            );
            state.RequireForUpdate<EnemyWavesRef>();
            state.RequireForUpdate<SpawnerData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            // GPUI manager (optional, managed; do not store on struct)
            GPUIPrefabManager gpuiMgr = null;
            try { gpuiMgr = SystemAPI.ManagedAPI.GetSingleton<GPUIManagerRef>()?.Value; } catch { }

            var wavesRef = SystemAPI.ManagedAPI.GetSingleton<EnemyWavesRef>()?.Waves;
            if (wavesRef == null || wavesRef.waves == null || wavesRef.waves.Count == 0) return;

            // Ensure runtime singleton exists
            var runtimeEnt = GetOrCreateSingleton(ref state);
            var rt = em.GetComponentData<EnemyWaveRuntime>(runtimeEnt);

            // If not active, arm
            if (rt.Active == 0)
            {
                rt = new EnemyWaveRuntime { WaveIndex = 0, WaveElapsed = 0f, InterDelayRemaining = 0f, Active = 1 };
                em.SetComponentData(runtimeEnt, rt);
                InitCursorsForWave(ref state, runtimeEnt, wavesRef, 0);
            }

            float dt = (float)SystemAPI.Time.DeltaTime;
            var data = SystemAPI.ManagedAPI.GetSingleton<SpawnerData>();

            // Collect enemy spawn centers
            var centers = GatherCenters(ref state, SpawnerType.Enemy);

            if (rt.InterDelayRemaining > 0f)
            {
                rt.InterDelayRemaining = max(0f, rt.InterDelayRemaining - dt);
                em.SetComponentData(runtimeEnt, rt);
                return;
            }

            if (rt.WaveIndex >= wavesRef.waves.Count)
            {
                if (!wavesRef.loop) return;
                rt.WaveIndex = 0;
                rt.WaveElapsed = 0f;
                em.SetComponentData(runtimeEnt, rt);
                InitCursorsForWave(ref state, runtimeEnt, wavesRef, rt.WaveIndex);
            }

            var wave = wavesRef.waves[rt.WaveIndex];
            float remainingWave = max(0f, wave.durationSeconds - rt.WaveElapsed);

            // Drive entry cursors
            var cursors = em.GetBuffer<EnemyWaveEntryCursor>(runtimeEnt);
            int totalEntries = wave.entries.Count;

            float clampDt = min(dt, remainingWave);
            bool anyImmediateAdds = false;

            for (int i = 0; i < totalEntries; i++)
            {
                ref var cur = ref cursors.ElementAt(i);
                if (cur.Remaining <= 0 || cur.RatePerSec <= 0f || cur.Window <= 0f) continue;

                // Pause this entry after its individual window
                float windowElapsed = rt.WaveElapsed;
                if (windowElapsed > cur.Window) continue;

                cur.Accum += cur.RatePerSec * clampDt;
                int spawnNow = (int)floor(cur.Accum);
                if (spawnNow <= 0) continue;

                int toSpawn = min(spawnNow, cur.Remaining);
                cur.Accum -= toSpawn;

                for (int s = 0; s < toSpawn; s++)
                {
                    var entry = wave.entries[i];

                    // choose center + jitter in SpawnerData area
                    Vector3 center = centers.Count > 0 ? centers[(int)(SystemAPI.Time.ElapsedTime * 997 + i + s) % centers.Count] : Vector3.zero;
                    Vector3 pos = RandomInArea(data.SpawnAreaFrom, data.SpawnAreaTo) + center;

                    anyImmediateAdds |= SpawnUnitPrefab(ref state, data.EntityPrefab, entry.unitPrefab, pos, Constants.GameConstants.ENEMY_FACTION, gpuiMgr);
                }

                cur.Remaining -= toSpawn;
            }

            // Advance wave timer
            rt.WaveElapsed += dt;
            if (rt.WaveElapsed >= wave.durationSeconds)
            {
                rt.WaveIndex++;
                rt.WaveElapsed = 0f;
                rt.InterDelayRemaining = max(0f, wavesRef.interWaveDelaySeconds);
                em.SetComponentData(runtimeEnt, rt);

                if (rt.WaveIndex < wavesRef.waves.Count) InitCursorsForWave(ref state, runtimeEnt, wavesRef, rt.WaveIndex);
            }
            else
            {
                em.SetComponentData(runtimeEnt, rt);
            }

            if (anyImmediateAdds && gpuiMgr != null)
                GPUIPrefabAPI.UpdateTransformData(gpuiMgr);
        }

        // --- helpers ---
        private Entity GetOrCreateSingleton(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SystemAPI.TryGetSingletonEntity<EnemyWaveRuntime>(out var e))
            {
                e = em.CreateEntity(typeof(EnemyWaveRuntime), typeof(EnemyWaveEntryCursor));
                em.SetComponentData(e, new EnemyWaveRuntime { Active = 0 });
                var buf = em.GetBuffer<EnemyWaveEntryCursor>(e);
                buf.Clear();
            }

            return e;
        }

        private void InitCursorsForWave(ref SystemState state, Entity runtime, EnemyWavesDefinition def, int waveIndex)
        {
            var em = state.EntityManager;
            var buf = em.GetBuffer<EnemyWaveEntryCursor>(runtime);
            buf.Clear();
            var wave = def.waves[waveIndex];

            for (int i = 0; i < wave.entries.Count; i++)
            {
                var entry = wave.entries[i];
                float rate = entry.windowSeconds > 0f ? (entry.count / entry.windowSeconds) : 0f;
                buf.Add(
                    new EnemyWaveEntryCursor
                    {
                        Remaining = math.max(0, entry.count),
                        Accum = 0f,
                        RatePerSec = rate,
                        Window = math.max(0.0001f, entry.windowSeconds)
                    }
                );
            }
        }

        private List<Vector3> GatherCenters(ref SystemState state, SpawnerType type)
        {
            var centers = new List<Vector3>(8);
            var em = state.EntityManager;
            using var ents = _markerQ.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var m = em.GetComponentData<SpawnerMarker>(e);
                if (m.Type != type) continue;
                var lt = em.GetComponentData<LocalTransform>(e);
                centers.Add(lt.Position);
            }

            return centers;
        }

        private static Vector3 RandomInArea(Vector3 from, Vector3 to)
        {
            float rx = UnityEngine.Random.Range(min(from.x, to.x), max(from.x, to.x));
            float ry = UnityEngine.Random.Range(min(from.y, to.y), max(from.y, to.y));
            float rz = UnityEngine.Random.Range(min(from.z, to.z), max(from.z, to.z));
            return new Vector3(rx, ry, rz);
        }

        private static bool SpawnUnitPrefab(ref SystemState state, Entity unitEntityPrefab, GameObject bodyPrefab, Vector3 pos, byte faction, GPUIPrefabManager gpuiManager)
        {
            var em = state.EntityManager;
            var e = em.Instantiate(unitEntityPrefab);
            em.SetComponentData(e, LocalTransform.FromPosition(pos));

            var go = Object.Instantiate(bodyPrefab, pos, Quaternion.identity);

            var brain = go.GetComponent<UnitBrain>();
            brain.SetEntity(e);

            // Hard requirement: UnitStatic must be applied
            UnitStaticSetup.Apply(em, e, brain);

            bool usedImmediate = false;
            var gpui = go.GetComponent<GPUIPrefab>();
            if (gpui != null)
            {
                if (gpuiManager != null)
                {
                    int proto = gpuiManager.GetPrototypeIndex(bodyPrefab);
                    if (proto < 0) proto = GPUIPrefabAPI.AddPrototype(gpuiManager, bodyPrefab);
                    usedImmediate = GPUIPrefabAPI.AddPrefabInstanceImmediate(gpuiManager, gpui, proto) >= 0;
                }

                if (!usedImmediate) GPUIPrefabAPI.AddPrefabInstance(gpui);
            }

            PrimeAgentEntity(em, e, faction);
            return usedImmediate;
        }

        private static void PrimeAgentEntity(EntityManager em, Entity e, byte faction)
        {
            em.AddComponent(e, ComponentType.ReadOnly<AgentTag>());
            em.AddComponentData(e, new SpatialHashTarget { Faction = faction });

            if (faction == Constants.GameConstants.ALLY_FACTION)
                em.AddComponent<AllyTag>(e);
            else
                em.AddComponent<EnemyTag>(e);

            em.AddComponentData(e, new Target { Value = Entity.Null });
            em.AddComponentData(e, new DesiredDestination { Position = float3.zero, HasValue = 0 });
            em.AddComponentData(e, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });
            em.AddComponentData(e, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
            em.AddComponentData(e, new Alive { Value = 1 });

            em.AddComponentData(e, new AttackCooldown { NextTime = 0f });
            em.AddComponentData(e, new AttackWindup { Active = 0, ReleaseTime = 0f });
            if (!em.HasComponent<AttackRequest>(e))
            {
                em.AddComponent<AttackRequest>(e);
                em.SetComponentEnabled<AttackRequest>(e, false);
            }

            if (!em.HasComponent<CastRequest>(e))
            {
                em.AddComponent<CastRequest>(e);
                em.SetComponentEnabled<CastRequest>(e, false);
            }

            if (!em.HasComponent<SpellProjectileSpawnRequest>(e))
            {
                em.AddComponent<SpellProjectileSpawnRequest>(e);
                em.SetComponentEnabled<SpellProjectileSpawnRequest>(e, false);
            }

            if (!em.HasComponent<EcsProjectileSpawnRequest>(e))
            {
                em.AddComponent<EcsProjectileSpawnRequest>(e);
                em.SetComponentEnabled<EcsProjectileSpawnRequest>(e, false);
            }

            if (!em.HasComponent<MeleeHitRequest>(e))
            {
                em.AddComponent<MeleeHitRequest>(e);
                em.SetComponentEnabled<MeleeHitRequest>(e, false);
            }

            if (!em.HasComponent<SpellState>(e)) em.AddComponentData(e, new SpellState { CanCast = 1, Ready = 1 });
            if (!em.HasComponent<SpellCooldown>(e)) em.AddComponentData(e, new SpellCooldown { NextTime = 0f });
            if (!em.HasComponent<SpellWindup>(e)) em.AddComponentData(e, new SpellWindup { Active = 0, ReleaseTime = 0f });
        }
    }
}
