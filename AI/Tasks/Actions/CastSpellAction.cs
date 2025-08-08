using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Casting unit spell")]
    public class CastSpellAction : AbstractTaskAction<CastSpellComponent, CastSpellTag, CastSpellSystem>, IAction
    {
        protected override CastSpellComponent CreateBufferElement(ushort runtimeIndex) { return new CastSpellComponent { Index = runtimeIndex }; }
    }

    public struct CastSpellComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct CastSpellTag : IComponentData, IEnableableComponent
    {
    }

    // [DisableAutoCreation]
    // public partial class CastSpellSystem : SystemBase
    // {
    //     protected override void OnUpdate()
    //     {
    //         var queue = new NativeQueue<Entity>(Allocator.TempJob);
    //         var queueWriter = queue.AsParallelWriter();
    //
    //         var handle = Entities
    //             .WithAll<CastSpellTag>()
    //             .WithNativeDisableParallelForRestriction(queueWriter)
    //             .ForEach((Entity entity,
    //                 ref DynamicBuffer<TaskComponent> tasks,
    //                 ref DynamicBuffer<CastSpellComponent> buffer) =>
    //             {
    //                 for (int i = 0; i < buffer.Length; i++)
    //                 {
    //                     var cmd = buffer[i];
    //                     var task = tasks[cmd.Index];
    //
    //                     if (task.Status == TaskStatus.Queued)
    //                     {
    //                         task.Status = TaskStatus.Running;
    //                         tasks[cmd.Index] = task;
    //                     }
    //                     else if (task.Status == TaskStatus.Running) { queueWriter.Enqueue(entity); }
    //                 }
    //             }).ScheduleParallel(Dependency);
    //
    //         handle.Complete();
    //
    //         while (queue.TryDequeue(out var entity))
    //         {
    //             if (!EntityManager.HasComponent<UnitBrainRef>(entity)) continue;
    //
    //             var brainRef = EntityManager.GetSharedComponentManaged<UnitBrainRef>(entity);
    //             if (brainRef.Value == null) continue;
    //             var tasks = EntityManager.GetBuffer<TaskComponent>(entity);
    //             var buffer = EntityManager.GetBuffer<CastSpellComponent>(entity);
    //
    //             foreach (var cmd in buffer)
    //             {
    //                 var task = tasks[cmd.Index];
    //                 if (task.Status != TaskStatus.Running) continue;
    //
    //                 var target = brainRef.Value.CurrentTarget;
    //                 if (target != null)
    //                 {
    //                     brainRef.Value.TryCastSpell();
    //                     Debug.Log($"[CastSpellSystem] Entity {entity.Index} Casting spell on {target.name}");
    //                 }
    //
    //                 task.Status = target ? TaskStatus.Success : TaskStatus.Failure;
    //                 tasks[cmd.Index] = task;
    //             }
    //         }
    //
    //         queue.Dispose();
    //     }
    // }
    
    [DisableAutoCreation]
    public partial class CastSpellSystem
        : TaskProcessorSystem<CastSpellComponent, CastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var target = brain.CurrentTarget;

            if (target != null)
            {
                brain.TryCastSpell();
#if UNITY_EDITOR
                Debug.Log($"[CastSpellSystem] {e.Index} cast spell on {target.name}");
#endif
            }

            return target ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}