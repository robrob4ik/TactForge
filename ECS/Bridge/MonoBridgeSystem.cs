using GPUInstancerPro.PrefabModule;
using OneBitRob.AI;
using OneBitRob.ECS;
using OneBitRob.ECS.GPUI;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.Bridge
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AITaskSystemGroup))]
    public partial class MonoBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;

            // DesiredDestination -> Mono (navigation)
            foreach (var (dd, e) in SystemAPI.Query<RefRW<DesiredDestination>>().WithEntityAccess())
            {
                if (dd.ValueRO.HasValue == 0) continue;
                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    // ─── Movement lock while casting ────────────────────────────
                    bool casting = em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0;

                    Vector3 wanted = casting
                        ? brain.transform.position                      // stop at current spot
                        : (Vector3)dd.ValueRO.Position;                 // normal desired destination

                    if ((wanted - brain.CurrentTargetPosition).sqrMagnitude > 0.0004f)
                        brain.MoveToPosition(wanted);

#if UNITY_EDITOR
                    Debug.DrawLine(brain.transform.position, wanted, casting ? new Color(1f, 0.6f, 0.1f, 0.9f) : Color.cyan, 0f, false);
#endif
                }
                dd.ValueRW = default;
            }

            // DesiredFacing -> Mono (rotation)
            foreach (var (df, e) in SystemAPI.Query<RefRW<DesiredFacing>>().WithEntityAccess())
            {
                if (df.ValueRO.HasValue == 0) continue;
                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    var facePos = (Vector3)df.ValueRO.TargetPosition;
                    brain.SetForcedFacing(facePos);
#if UNITY_EDITOR
                    Debug.DrawLine(brain.transform.position, facePos, Color.yellow, 0f, false);
#endif
                }
                df.ValueRW = default;
            }

            // Target (entity) -> Mono (GameObject)
            foreach (var (target, e) in SystemAPI.Query<RefRO<Target>>().WithEntityAccess())
            {
                var brain = UnitBrainRegistry.Get(e);
                if (!brain) continue;

                var targetBrain = UnitBrainRegistry.Get(target.ValueRO.Value);
                brain.CurrentTarget = targetBrain ? targetBrain.gameObject : null;
#if UNITY_EDITOR
                if (targetBrain) Debug.DrawLine(brain.transform.position, targetBrain.transform.position, Color.green, 0f, false);
#endif
            }

            // ─────────────────────────────────────────────────────────────────
            // WEAPON PROJECTILES (RANGED) — ECS -> Mono
            foreach (var (spawn, e) in SystemAPI.Query<RefRW<EcsProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawn.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain && brain.CombatSubsystem != null)
                {
                    var origin = (Vector3)spawn.ValueRO.Origin;
                    var dir    = ((Vector3)spawn.ValueRO.Direction).normalized;

                    int layerMask = brain.GetDamageableLayerMask().value;

                    brain.CombatSubsystem.FireProjectile(
                        origin,
                        dir,
                        brain.gameObject,
                        spawn.ValueRO.Speed,
                        spawn.ValueRO.Damage,
                        spawn.ValueRO.MaxDistance,
                        layerMask,
                        spawn.ValueRO.CritChance,
                        spawn.ValueRO.CritMultiplier
                    );

#if UNITY_EDITOR
                    Debug.DrawRay(origin, dir * 1.2f, Color.red, 0f, false);
#endif
                }

                // consume
                spawn.ValueRW = default;
            }

            // ─────────────────────────────────────────────────────────────────
            // SPELL PROJECTILES — ECS -> Mono
            foreach (var (spawn, e) in SystemAPI.Query<RefRW<SpellProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawn.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain && brain.CombatSubsystem != null)
                {
                    var origin = (Vector3)spawn.ValueRO.Origin;
                    var dir    = ((Vector3)spawn.ValueRO.Direction).normalized;
                    string projId = SpellVisualRegistry.GetProjectileId(spawn.ValueRO.ProjectileIdHash);

                    brain.CombatSubsystem.FireSpellProjectile(
                        projId,
                        origin,
                        dir,
                        brain.gameObject,
                        spawn.ValueRO.Speed,
                        spawn.ValueRO.Damage,
                        spawn.ValueRO.MaxDistance,
                        spawn.ValueRO.LayerMask,
                        spawn.ValueRO.Radius,
                        spawn.ValueRO.Pierce == 1
                    );
#if UNITY_EDITOR
                    Debug.DrawRay(origin, dir * 1.2f, Color.magenta, 0f, false);
#endif
                }

                spawn.ValueRW = default;
            }

            // ─────────────────────────────────────────────────────────────────
            // SUMMONS — ECS -> Mono (unchanged)
            foreach (var (req, e) in SystemAPI.Query<RefRW<SummonRequest>>().WithEntityAccess())
            {
                if (req.ValueRO.HasValue == 0) continue;

                var prefab = SpellVisualRegistry.GetSummonPrefab(req.ValueRO.PrefabIdHash);
                if (prefab != null)
                {
                    var data   = SystemAPI.ManagedAPI.TryGetSingleton<SpawnerData>(out var found) ? SystemAPI.ManagedAPI.GetSingleton<SpawnerData>() : null;
                    var gpuiMgr= SystemAPI.ManagedAPI.TryGetSingleton<GPUIManagerRef>(out var ok) ? SystemAPI.ManagedAPI.GetSingleton<GPUIManagerRef>().Value : null;

                    if (data != null && gpuiMgr != null)
                    {
                        for (int i = 0; i < math.max(1, req.ValueRO.Count); i++)
                        {
                            var brainEnt = em.Instantiate(data.EntityPrefab);
                            var pos = req.ValueRO.Position + new float3(UnityEngine.Random.Range(-0.5f, 0.5f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f));
                            em.SetComponentData(brainEnt, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));

                            var go = Object.Instantiate(prefab, (Vector3)pos, Quaternion.identity);
                            var gpui = go.GetComponent<GPUIPrefab>();
                            if (gpui) GPUIPrefabAPI.AddPrefabInstanceImmediate(gpuiMgr, gpui);

                            var monoBrain = go.GetComponent<UnitBrain>();
                            monoBrain?.SetEntity(brainEnt);

                            em.AddComponent(brainEnt, ComponentType.ReadOnly<AgentTag>());
                            em.AddComponentData(brainEnt, new SpatialHashComponents.SpatialHashTarget { Faction = req.ValueRO.Faction });
                            if (req.ValueRO.Faction == OneBitRob.Constants.GameConstants.ALLY_FACTION) em.AddComponent<AllyTag>(brainEnt);
                            else if (req.ValueRO.Faction == OneBitRob.Constants.GameConstants.ENEMY_FACTION) em.AddComponent<EnemyTag>(brainEnt);

                            em.AddComponentData(brainEnt, new Target { Value = Entity.Null });
                            em.AddComponentData(brainEnt, new DesiredDestination { Position = pos, HasValue = 0 });
                            em.AddComponentData(brainEnt, new DesiredFacing { TargetPosition = float3.zero, HasValue = 0 });
                            em.AddComponentData(brainEnt, new InAttackRange { Value = 0, DistanceSq = float.PositiveInfinity });
                            em.AddComponentData(brainEnt, new Alive { Value = 1 });

                            int hp = monoBrain != null && monoBrain.UnitDefinition != null ? monoBrain.UnitDefinition.health : 100;
                            em.AddComponentData(brainEnt, new HealthMirror { Current = hp, Max = hp });

                            byte style = 1;
                            var weapon = monoBrain != null ? monoBrain.UnitDefinition?.weapon : null;
                            if (weapon is RangedWeaponDefinition) style = 2;
                            em.AddComponentData(brainEnt, new CombatStyle { Value = style });

                            em.AddComponentData(brainEnt, new SpellState { CanCast = 1, Ready = 1 });
                            em.AddComponentData(brainEnt, new AttackRequest { Target = Entity.Null, HasValue = 0 });
                            em.AddComponentData(brainEnt, new AttackCooldown { NextTime = 0f });
                            em.AddComponentData(brainEnt, new AttackWindup { Active = 0, ReleaseTime = 0f });
                            em.AddComponentData(brainEnt, new CastRequest { Kind = CastKind.None, Target = Entity.Null, AoEPosition = float3.zero, HasValue = 0 });
                            em.AddComponentData(brainEnt, new RetargetCooldown { NextTime = 0 });
                        }
                    }
                }

                req.ValueRW = default;
            }
        }
    }
}
