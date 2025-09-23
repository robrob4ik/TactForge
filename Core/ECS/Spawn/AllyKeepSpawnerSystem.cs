using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.ECS.GPUI;
using Opsive.BehaviorDesigner.Runtime;
using Unity.Mathematics;
using Random = UnityEngine.Random;


namespace OneBitRob.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MonoToEcsSyncGroup))]
    public partial struct AllyKeepSpawnerSystem : ISystem
    {
        private EntityQuery _markerQ;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AllySpawnDefinitionRef>();
            state.RequireForUpdate<AllySpawnTimer>();
            state.RequireForUpdate<AllyKeepConfig>(); // NEW

            _markerQ = state.GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<SpawnerMarker>(), ComponentType.ReadOnly<LocalTransform>() }
                }
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            float dt = (float)SystemAPI.Time.DeltaTime;

            var setRef = SystemAPI.ManagedAPI.GetSingleton<AllySpawnDefinitionRef>();
            var set = setRef?.AllySpawnDefinition;
            if (set == null || set.entries == null || set.entries.Count == 0) return;

            var cfg = SystemAPI.GetSingleton<AllyKeepConfig>(); // NEW

            var timerEnt = SystemAPI.GetSingletonEntity<AllySpawnTimer>();
            var timer = em.GetComponentData<AllySpawnTimer>(timerEnt);
            timer.Elapsed += dt;

            float period = math.max(1f, set.periodSeconds);
            if (timer.Elapsed + 1e-5f < period)
            {
                em.SetComponentData(timerEnt, timer);
                return;
            }

            GPUIPrefabManager gpuiMgr = null;
            try { gpuiMgr = SystemAPI.ManagedAPI.GetSingleton<GPUIManagerRef>()?.Value; } catch { }

            var centers = GatherAllyCenters(ref state);

            bool anyImmediateAdds = false;
            bool spawnedAny = false;

            for (int entryIndex = 0; entryIndex < set.entries.Count; entryIndex++)
            {
                var cfgEntry = set.entries[entryIndex];
                if (cfgEntry.unitPrefab == null || cfgEntry.count <= 0) continue;

                for (int i = 0; i < cfgEntry.count; i++)
                {
                    Vector3 center = centers.Count > 0 ? centers[(i + entryIndex) % centers.Count] : Vector3.zero;
                    Vector3 pos = RandomInArea(cfg.SpawnAreaFrom, cfg.SpawnAreaTo) + center; // NEW

                    bool usedImmediate = SpawnUnitPrefab(
                        ref state,
                        cfg.AgentEntityPrefab, // NEW
                        cfgEntry.unitPrefab,
                        pos,
                        Constants.GameConstants.ALLY_FACTION,
                        gpuiMgr
                    );

                    anyImmediateAdds |= usedImmediate;
                    spawnedAny = true;
                }
            }

            if (anyImmediateAdds && gpuiMgr != null) GPUIPrefabAPI.UpdateTransformData(gpuiMgr);

            if (spawnedAny) BehaviorTree.EnableBakedBehaviorTreeSystem(World.DefaultGameObjectInjectionWorld);

            timer.Elapsed = 0f;
            em.SetComponentData(timerEnt, timer);
        }

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

        // CHANGED: area from float3
        private static Vector3 RandomInArea(float3 from, float3 to)
        {
            float rx = UnityEngine.Random.Range(math.min(from.x, to.x), math.max(from.x, to.x));
            float ry = UnityEngine.Random.Range(math.min(from.y, to.y), math.max(from.y, to.y));
            float rz = UnityEngine.Random.Range(math.min(from.z, to.z), math.max(from.z, to.z));
            return new Vector3(rx, ry, rz);
        }

        private static bool SpawnUnitPrefab(
            ref SystemState state,
            Entity agentEntityPrefab,           // NEW name
            GameObject bodyPrefab,
            Vector3 pos,
            byte faction,
            GPUIPrefabManager gpuiManager)
        {
            var em = state.EntityManager;

            var e = em.Instantiate(agentEntityPrefab); // NEW
            em.SetComponentData(e, LocalTransform.FromPosition(pos));

            var go = Object.Instantiate(bodyPrefab, pos, Quaternion.identity);
            var brain = go.GetComponent<UnitBrain>();
            brain.SetEntity(e);

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