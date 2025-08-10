using OneBitRob.AI;
using OneBitRob.ECS;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.Bridge
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AITaskSystemGroup))]
    public partial class MonoBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 1) Desired destination -> Mono
            foreach (var (dd, e) in SystemAPI.Query<RefRW<DesiredDestination>>().WithEntityAccess())
            {
                if (dd.ValueRO.HasValue == 0) continue;
                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    var wanted = (Vector3)dd.ValueRO.Position;
                    if ((wanted - brain.CurrentTargetPosition).sqrMagnitude > 0.0004f)
                        brain.MoveToPosition(wanted);
#if UNITY_EDITOR
                    Debug.DrawLine(brain.transform.position, wanted, Color.cyan, 0f, false);
#endif
                }
                dd.ValueRW = default; // consume
            }

            // 2) Desired facing -> Mono (use cached ability via UnitBrain)
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
                df.ValueRW = default; // consume
            }

            // 3) Sync Target -> UnitBrain.CurrentTarget (read-only mapping)
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

            // 4) Fire pooled projectiles requested by ECS
            foreach (var (spawn, e) in SystemAPI.Query<RefRW<EcsProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawn.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    var shooter = brain.GetComponent<EcsRangedShooter>();
#if UNITY_EDITOR
                    if (shooter == null)
                        Debug.LogWarning($"[{brain.name}] Missing EcsRangedShooter for ranged ECS attack.");
#endif
                    if (shooter != null)
                    {
                        var origin = (Vector3)spawn.ValueRO.Origin;
                        var dir    = ((Vector3)spawn.ValueRO.Direction).normalized;
                        shooter.Fire(origin, dir, brain.gameObject);
                    }
                }

                spawn.ValueRW = default; // consume
            }
        }
    }
}
