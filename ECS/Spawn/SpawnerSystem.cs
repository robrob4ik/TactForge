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

            foreach (var pos in allySpawnCenter) SpawnGroup(ref state, data.EntityPrefab, data.AllyPrefabs, data.UnitsSpawnCount, pos, data, Constants.GameConstants.ALLY_FACTION);
            foreach (var pos in enemySpawnCenter) SpawnGroup(ref state, data.EntityPrefab, data.EnemyPrefabs, data.UnitsSpawnCount, pos, data, Constants.GameConstants.ENEMY_FACTION);

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

                    state.EntityManager.AddComponent(e, ComponentType.ReadOnly<AgentTag>());
                    state.EntityManager.AddComponentData(e, new SpatialHashComponents.SpatialHashTarget { Faction = faction });

                    if (faction == Constants.GameConstants.ALLY_FACTION)
                        state.EntityManager.AddComponent<AllyTag>(e);
                    else if (faction == Constants.GameConstants.ENEMY_FACTION) state.EntityManager.AddComponent<EnemyTag>(e);

                    state.EntityManager.AddComponentData(e, new Target { Value = Entity.Null });

                    state.EntityManager.AddComponentData(e, new DesiredDestination { Position = float3.zero, HasValue = 0 });
                    state.EntityManager.AddComponentData(e, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });

                    state.EntityManager.AddComponentData(e, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
                    state.EntityManager.AddComponentData(e, new Alive { Value = 1 });

                    int hp = unitBrainMono.UnitDefinition != null ? unitBrainMono.UnitDefinition.health : 100;
                    state.EntityManager.AddComponentData(e, new HealthMirror { Current = hp, Max = hp });

                    byte style = 1;
                    var weapon = unitBrainMono.UnitDefinition != null ? unitBrainMono.UnitDefinition.weapon : null;
                    if (weapon is RangedWeaponDefinition) style = 2;
                    state.EntityManager.AddComponentData(e, new CombatStyle { Value = style });

                    state.EntityManager.AddComponentData(e, new SpellState { CanCast = 1, Ready = 1 });
                    state.EntityManager.AddComponentData(e, new AttackRequest { Target = Entity.Null, HasValue = 0 });
                    state.EntityManager.AddComponentData(e, new AttackCooldown { NextTime = 0f });
                    state.EntityManager.AddComponentData(e, new AttackWindup { Active = 0, ReleaseTime = 0f });
                    state.EntityManager.AddComponentData(e, new CastRequest { Kind = CastKind.None, Target = Entity.Null, AoEPosition = float3.zero, HasValue = 0 });

                    // ─────────────────────────────────────────────────────────────
                    // Spells (null-safe) – uses ONLY the first spell just like before
                    var spells = unitBrainMono.UnitDefinition != null ? unitBrainMono.UnitDefinition.unitSpells : null;
                    var hasSpell = spells != null && spells.Count > 0 && spells[0] != null;
                    if (hasSpell)
                    {
                        var spell = spells[0];

                        int projHash = SpellVisualRegistry.RegisterProjectile(spell.ProjectileId);
                        int vfxHash = SpellVisualRegistry.RegisterVfx(spell.EffectVfxId);
                        int areaVfxHash = SpellVisualRegistry.RegisterVfx(spell.AreaVfxId);
                        int summonHash = SpellVisualRegistry.RegisterSummon(spell.SummonPrefab);

                        float amount = (spell.Kind == SpellKind.EffectOverTimeArea || spell.Kind == SpellKind.EffectOverTimeTarget)
                            ? spell.TickAmount
                            : spell.EffectAmount;

                        state.EntityManager.AddComponentData(
                            e, new SpellConfig
                            {
                                Kind = spell.Kind,
                                EffectType = spell.EffectType,
                                AcquireMode = spell.AcquireMode,

                                CastTime = spell.FireDelaySeconds,
                                Cooldown = spell.Cooldown,
                                Range = spell.Range,
                                RequiresLineOfSight = 0,
                                TargetLayerMask = 0,

                                RequireFacing = 0,
                                FaceToleranceDeg = 0f,
                                MaxExtraFaceDelay = 0f,

                                Amount = amount,

                                ProjectileSpeed = spell.ProjectileSpeed,
                                ProjectileMaxDistance = spell.ProjectileMaxDistance,
                                ProjectileRadius = spell.ProjectileRadius,
                                ProjectileIdHash = projHash,
                                MuzzleForward = spell.MuzzleForward,
                                MuzzleLocalOffset = new float3(spell.MuzzleLocalOffset.x, spell.MuzzleLocalOffset.y, spell.MuzzleLocalOffset.z),

                                // ✅ FIX: The AOE DAMAGE RADIUS must come from SpellDefinition.AreaRadius (NOT Range)
                                AreaRadius = spell.Kind == SpellKind.EffectOverTimeArea ? spell.AreaRadius : 0f,
                                Duration = spell.Duration,
                                TickInterval = spell.TickInterval,
                                EffectVfxIdHash = vfxHash,
                                AreaVfxIdHash = areaVfxHash,
                                AreaVfxYOffset = spell.AreaVfxYOffset, // NEW

                                ChainMaxTargets = spell.ChainMaxTargets,
                                ChainRadius = spell.ChainRadius,
                                ChainJumpDelay = spell.ChainPerJumpDelay,

                                SummonPrefabHash = summonHash
                            }
                        );

                        state.EntityManager.AddComponentData(e, new SpellDecisionRequest { HasValue = 0 });
                        state.EntityManager.AddComponentData(e, new SpellWindup { Active = 0, ReleaseTime = 0f });
                        state.EntityManager.AddComponentData(e, new SpellCooldown { NextTime = 0f });
                    }

                    // ─────────────────────────────────────────────────────────────
                    // Retarget throttle — GUARDED add
                    if (!state.EntityManager.HasComponent<RetargetCooldown>(e)) state.EntityManager.AddComponentData(e, new RetargetCooldown { NextTime = 0 });

                    state.EntityManager.AddComponentData(
                        e, new RetargetAssist
                        {
                            LastPos = pos,
                            LastDistSq = float.MaxValue,
                            NoProgressTime = 0f
                        }
                    );
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