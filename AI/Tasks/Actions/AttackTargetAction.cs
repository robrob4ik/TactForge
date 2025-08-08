using OneBitRob.Constants;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Finds a valid target via UnitBrain strategy")]
    public class AttackTargetAction : AbstractTaskAction<AttackTargetComponent, AttackTargetTag, AttackTargetSystem>, IAction
    {
        protected override AttackTargetComponent CreateBufferElement(ushort runtimeIndex) { return new AttackTargetComponent { Index = runtimeIndex }; }
    }

    public struct AttackTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct AttackTargetTag : IComponentData, IEnableableComponent
    {
    }

    // [DisableAutoCreation]
    // public partial class AttackTargetSystem : SystemBase
    // {
    //     protected override void OnUpdate()
    //     {
    //         var queue = new NativeQueue<Entity>(Allocator.TempJob);
    //         var queueWriter = queue.AsParallelWriter();
    //
    //         var handle = Entities
    //             .WithAll<AttackTargetTag>()
    //             .WithNativeDisableParallelForRestriction(queueWriter)
    //             .ForEach((Entity entity,
    //                 ref DynamicBuffer<TaskComponent> tasks,
    //                 ref DynamicBuffer<AttackTargetComponent> buffer) =>
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
    //             var buffer = EntityManager.GetBuffer<AttackTargetComponent>(entity);
    //
    //             foreach (var cmd in buffer)
    //             {
    //                 var task = tasks[cmd.Index];
    //                 if (task.Status != TaskStatus.Running) continue;
    //                 
    //                 var target = brainRef.Value.CurrentTarget;
    //                 if (target != null)
    //                 {
    //                     brainRef.Value.Attack(target.transform);
    //                     EnigmaLogger.Log($"[AttackTargetSystem] Entity {entity.Index} attacking {target.name}");
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
    public partial class AttackTargetSystem
        : TaskProcessorSystem<AttackTargetComponent, AttackTargetTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var target = brain.CurrentTarget;

            if (target != null)
            {
                brain.Attack(target.transform);
                EcsLogger.Info(this, $"[{e.Index}] attacking {target.name}");
            }

            return target ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}