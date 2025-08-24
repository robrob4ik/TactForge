// // FILE: OneBitRob/AI/RotateToSpellTargetAction.cs (ECB-safe)
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
// using Opsive.BehaviorDesigner.Runtime.Tasks;
// using Opsive.GraphDesigner.Runtime;
// using UnityEngine;
// using OneBitRob.ECS;
//
// namespace OneBitRob.AI
// {
//     [NodeDescription("Rotate to planned spell aim (Target/AoE)")]
//     public class RotateToSpellTargetAction
//         : AbstractTaskAction<RotateToSpellTargetComponent, RotateToSpellTargetTag, RotateToSpellTargetSystem>, IAction
//     {
//         protected override RotateToSpellTargetComponent CreateBufferElement(ushort runtimeIndex)
//             => new RotateToSpellTargetComponent { Index = runtimeIndex };
//     }
//
//     public struct RotateToSpellTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
//     public struct RotateToSpellTargetTag       : IComponentData, IEnableableComponent { }
//
//     [DisableAutoCreation]
//     [UpdateInGroup(typeof(AITaskSystemGroup))]
//     [UpdateAfter(typeof(PlanSpellTargetSystem))]   // plan ready
//     [UpdateBefore(typeof(CastSpellSystem))]        // rotate before commit
//     public partial class RotateToSpellTargetSystem
//         : TaskProcessorSystem<RotateToSpellTargetComponent, RotateToSpellTargetTag>
//     {
//         ComponentLookup<LocalTransform> _posRO;
//         EntityCommandBuffer _ecb;
//
//         protected override void OnCreate()
//         {
//             base.OnCreate();
//             _posRO = GetComponentLookup<LocalTransform>(true);
//         }
//
//         protected override void OnUpdate()
//         {
//             _posRO.Update(this);
//             _ecb = new EntityCommandBuffer(Allocator.Temp);
//
//             base.OnUpdate();
//
//             _ecb.Playback(EntityManager);
//             _ecb.Dispose();
//         }
//
//         protected override TaskStatus Execute(Entity e, UnitBrain brain)
//         {
//             var em = EntityManager;
//
//             Vector3? aim = null;
//
//             if (em.HasComponent<PlannedCast>(e))
//             {
//                 var plan = em.GetComponentData<PlannedCast>(e);
//                 if (plan.HasValue != 0)
//                 {
//                     if (plan.Kind == CastKind.AreaOfEffect) aim = (Vector3)plan.AoEPosition;
//                     else if (plan.Kind == CastKind.SingleTarget && plan.Target != Entity.Null && _posRO.HasComponent(plan.Target))
//                         aim = (Vector3)_posRO[plan.Target].Position;
//                 }
//             }
//
//             if (!aim.HasValue) return TaskStatus.Failure;
//
//             var df = new DesiredFacing { TargetPosition = (float3)aim.Value, HasValue = 1 };
//             if (em.HasComponent<DesiredFacing>(e)) _ecb.SetComponent(e, df);
//             else                                     _ecb.AddComponent(e, df);
//
// #if UNITY_EDITOR
//             Debug.DrawLine(brain.transform.position, aim.Value, Color.yellow, 0f, false);
// #endif
//             return TaskStatus.Success;
//         }
//     }
// }
