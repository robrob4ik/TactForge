// FILE: OneBitRob/Bridge/MonoBridgeSystem.cs

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
                dd.ValueRW = default;
            }

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

            foreach (var (spawn, e) in SystemAPI.Query<RefRW<EcsProjectileSpawnRequest>>().WithEntityAccess())
            {
                if (spawn.ValueRO.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain && brain.CombatSubsystem != null)
                {
                    var origin = (Vector3)spawn.ValueRO.Origin;
                    var dir    = ((Vector3)spawn.ValueRO.Direction).normalized;

                    brain.CombatSubsystem.FireProjectile(
                        origin,
                        dir,
                        brain.gameObject,
                        spawn.ValueRO.Speed,
                        spawn.ValueRO.Damage,
                        spawn.ValueRO.MaxDistance
                    );
#if UNITY_EDITOR
                    Debug.DrawRay(origin, dir * 1.2f, Color.red, 0f, false);
#endif
                }

                spawn.ValueRW = default;
            }
        }
    }
}
