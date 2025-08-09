// File: OneBitRob/Bridge/MonoBridgeSystem.cs
using OneBitRob.AI;
using OneBitRob.ECS;
using OneBitRob.EnigmaEngine;
using Opsive.BehaviorDesigner.Runtime.Groups;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.Bridge
{
    // Run in Simulation, strictly AFTER our AI tasks (which themselves run after BT).
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AITaskSystemGroup))]
    public partial class MonoBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = UnityEngine.Time.deltaTime;

            // 1) Apply movement destination -> Mono
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

            // 2) Desired facing -> Mono
            foreach (var (df, e) in SystemAPI.Query<RefRW<DesiredFacing>>().WithEntityAccess())
            {
                if (df.ValueRO.HasValue == 0) continue;
                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    var facePos = (Vector3)df.ValueRO.TargetPosition;
                    brain.Character
                         .FindAbility<EnigmaCharacterAgentsNavigationMovement>()
                         .ForcedRotationTarget = facePos;
#if UNITY_EDITOR
                    Debug.DrawLine(brain.transform.position, facePos, Color.yellow, 0f, false);
#endif
                }
                df.ValueRW = default; // consume
            }

            // 3) Sync Target -> UnitBrain.CurrentTarget
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

            // 4) Execute AttackRequests
            foreach (var (req, e) in SystemAPI.Query<RefRW<AttackRequest>>().WithEntityAccess())
            {
                if (req.ValueRO.HasValue == 0) continue;
                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    var tgtBrain = UnitBrainRegistry.Get(req.ValueRO.Target);
                    if (tgtBrain) brain.Attack(tgtBrain.transform);
                }
                req.ValueRW = default;
            }

            // 5) Execute CastRequests
            foreach (var (req, e) in SystemAPI.Query<RefRW<CastRequest>>().WithEntityAccess())
            {
                if (req.ValueRO.HasValue == 0) continue;
                var brain = UnitBrainRegistry.Get(e);
                if (brain)
                {
                    switch (req.ValueRO.Kind)
                    {
                        case CastKind.SingleTarget:
                            {
                                var tgt = UnitBrainRegistry.Get(req.ValueRO.Target);
                                if (tgt)
                                {
                                    brain.CurrentSpellTarget = tgt.gameObject;
                                    brain.TryCastSpell();
                                }
                                break;
                            }
                        case CastKind.AreaOfEffect:
                            {
                                brain.CurrentSpellTargetPosition = req.ValueRO.AoEPosition;
                                brain.TryCastSpell();
                                break;
                            }
                        case CastKind.MultiTarget:
                            break;
                    }
                }
                req.ValueRW = default;
            }
        }
    }
}
