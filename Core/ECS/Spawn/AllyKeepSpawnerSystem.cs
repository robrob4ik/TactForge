using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.ECS.GPUI;

using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MonoToEcsSyncGroup))]
    public partial struct AllyKeepSpawnerSystem : ISystem
    {
        private EntityQuery _markerQ; // OK to store

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AllySpawnSetRef>();
            state.RequireForUpdate<SpawnerData>();
            state.RequireForUpdate<AllySpawnTimer>();

            _markerQ = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<SpawnerMarker>(),
                    ComponentType.ReadOnly<LocalTransform>()
                }
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            float dt = (float)SystemAPI.Time.DeltaTime;

            var setRef = SystemAPI.ManagedAPI.GetSingleton<AllySpawnSetRef>();
            var set = setRef?.Set;
            if (set == null || set.entries == null || set.entries.Count == 0) return;

            var data = SystemAPI.ManagedAPI.GetSingleton<SpawnerData>();

            var timerEnt = SystemAPI.GetSingletonEntity<AllySpawnTimer>();
            var timer = em.GetComponentData<AllySpawnTimer>(timerEnt);
            timer.Elapsed += dt;

            float period = max(1f, set.periodSeconds);
            if (timer.Elapsed + 1e-5f < period)
            {
                em.SetComponentData(timerEnt, timer);
                return;
            }

            // Resolve GPUI manager when needed (managed; do not store on struct)
            GPUIPrefabManager gpuiMgr = null;
            try { gpuiMgr = SystemAPI.ManagedAPI.GetSingleton<GPUIManagerRef>()?.Value; } catch { /* optional */ }

            var centers = GatherAllyCenters(ref state);
            bool anyImmediateAdds = false;

            for (int entryIndex = 0; entryIndex < set.entries.Count; entryIndex++)
            {
                var cfg = set.entries[entryIndex];
                if (cfg.unitPrefab == null || cfg.count <= 0) continue;

                for (int i = 0; i < cfg.count; i++)
                {
                    Vector3 center = centers.Count > 0 ? centers[(i + entryIndex) % centers.Count] : Vector3.zero;
                    Vector3 pos = RandomInArea(data.SpawnAreaFrom, data.SpawnAreaTo) + center;

                    anyImmediateAdds |= SpawnUnitPrefab(
                        ref state,
                        data.EntityPrefab,
                        cfg.unitPrefab,
                        pos,
                        Constants.GameConstants.ALLY_FACTION,
                        gpuiMgr);
                }
            }

            if (anyImmediateAdds && gpuiMgr != null)
                GPUIPrefabAPI.UpdateTransformData(gpuiMgr);

            timer.Elapsed = 0f;
            em.SetComponentData(timerEnt, timer);
        }

        // -------- helpers (instance; no SystemAPI.Query here) --------

        private List<Vector3> GatherAllyCenters(ref SystemState state)
        {
            var centers = new List<Vector3>(4);
            var em = state.EntityManager;

            using var ents = _markerQ.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                var e = ents[i];
                var marker = em.GetComponentData<SpawnerMarker>(e);
                if (marker.Type != SpawnerType.Ally) continue;
                var lt = em.GetComponentData<LocalTransform>(e);
                centers.Add(lt.Position);
            }
            return centers;
        }

        private static Vector3 RandomInArea(Vector3 from, Vector3 to)
        {
            float rx = Random.Range(min(from.x, to.x), max(from.x, to.x));
            float ry = Random.Range(min(from.y, to.y), max(from.y, to.y));
            float rz = Random.Range(min(from.z, to.z), max(from.z, to.z));
            return new Vector3(rx, ry, rz);
        }

        private static bool SpawnUnitPrefab(
            ref SystemState state,
            Entity unitEntityPrefab,
            GameObject bodyPrefab,
            Vector3 pos,
            byte faction,
            GPUIPrefabManager gpuiManager)
        {
            var em = state.EntityManager;

            // Create ECS shell
            var e = em.Instantiate(unitEntityPrefab);
            em.SetComponentData(e, LocalTransform.FromPosition(pos));

            // Instantiate GO and wire brain
            var go = Object.Instantiate(bodyPrefab, pos, Quaternion.identity);
            var brain = go.GetComponent<UnitBrain>();
            brain.SetEntity(e);

            // Hard requirement: UnitStatic must be applied
            UnitStaticSetup.Apply(em, e, brain);

            // Register with GPUI if present
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

            // Prime ECS gameplay shell (mirrors your SpawnerSystem.PrimeAgentEntity)
            PrimeAgentEntity(em, e, faction);

            return usedImmediate;
        }

        private static void PrimeAgentEntity(EntityManager em, Entity e, byte faction)
        {
            em.AddComponent(e, ComponentType.ReadOnly<AgentTag>());
            em.AddComponentData(e, new SpatialHashTarget { Faction = faction });

            if (faction == Constants.GameConstants.ALLY_FACTION) em.AddComponent<AllyTag>(e);
            else em.AddComponent<EnemyTag>(e);

            em.AddComponentData(e, new Target { Value = Entity.Null });
            em.AddComponentData(e, new DesiredDestination { Position = float3.zero, HasValue = 0 });
            em.AddComponentData(e, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });
            em.AddComponentData(e, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
            em.AddComponentData(e, new Alive { Value = 1 });

            em.AddComponentData(e, new AttackCooldown { NextTime = 0f });
            em.AddComponentData(e, new AttackWindup { Active = 0, ReleaseTime = 0f });
            if (!em.HasComponent<AttackRequest>(e)) { em.AddComponent<AttackRequest>(e); em.SetComponentEnabled<AttackRequest>(e, false); }

            if (!em.HasComponent<CastRequest>(e))                 { em.AddComponent<CastRequest>(e);                 em.SetComponentEnabled<CastRequest>(e, false); }
            if (!em.HasComponent<SpellProjectileSpawnRequest>(e)) { em.AddComponent<SpellProjectileSpawnRequest>(e); em.SetComponentEnabled<SpellProjectileSpawnRequest>(e, false); }
            if (!em.HasComponent<EcsProjectileSpawnRequest>(e))   { em.AddComponent<EcsProjectileSpawnRequest>(e);   em.SetComponentEnabled<EcsProjectileSpawnRequest>(e, false); }
            if (!em.HasComponent<MeleeHitRequest>(e))             { em.AddComponent<MeleeHitRequest>(e);             em.SetComponentEnabled<MeleeHitRequest>(e, false); }

            if (!em.HasComponent<SpellState>(e))    em.AddComponentData(e, new SpellState { CanCast = 1, Ready = 1 });
            if (!em.HasComponent<SpellCooldown>(e)) em.AddComponentData(e, new SpellCooldown { NextTime = 0f });
            if (!em.HasComponent<SpellWindup>(e))   em.AddComponentData(e, new SpellWindup { Active = 0, ReleaseTime = 0f });
        }
    }
}
