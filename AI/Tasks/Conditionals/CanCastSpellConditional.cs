using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;

namespace OneBitRob.AI
{
    [NodeDescription("Uses DOTS to determine if unit can cast spell")]
    public class CanCastSpellConditional : AbstractTaskAction<CanCastSpellComponent, CanCastSpellTag, CanCastSpellSystem>, IConditional
    {
        protected override CanCastSpellComponent CreateBufferElement(ushort runtimeIndex) { return new CanCastSpellComponent { Index = runtimeIndex }; }
    }

    public struct CanCastSpellComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct CanCastSpellTag : IComponentData, IEnableableComponent
    {
    }

    // [DisableAutoCreation]
    // public partial class CanCastSpellTaskSystem : SystemBase
    // {
    //     protected override void OnUpdate()
    //     {
    //         var queue = new NativeQueue<Entity>(Allocator.TempJob);
    //         var queueWriter = queue.AsParallelWriter();
    //
    //         // Phase 1: Parallel ECS task preparation
    //         var handle = Entities
    //             .WithAll<CanCastSpellTag>()
    //             .WithNativeDisableParallelForRestriction(queueWriter)
    //             .ForEach((Entity entity,
    //                       ref DynamicBuffer<TaskComponent> tasks,
    //                       ref DynamicBuffer<CanCastSpellComponent> buffer) =>
    //             {
    //                 foreach (var cmd in buffer)
    //                 {
    //                     var task = tasks[cmd.Index];
    //
    //                     if (task.Status == TaskStatus.Queued)
    //                     {
    //                         task.Status = TaskStatus.Running;
    //                         tasks[cmd.Index] = task;
    //                     }
    //                     else if (task.Status == TaskStatus.Running)
    //                     {
    //                         queueWriter.Enqueue(entity);
    //                     }
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
    //             var canCastSpell = brainRef.Value.CanCastSpell();
    //             
    //             var tasks = EntityManager.GetBuffer<TaskComponent>(entity);
    //             var buffer = EntityManager.GetBuffer<CanCastSpellComponent>(entity);
    //
    //             foreach (var cmd in buffer)
    //             {
    //                 var task = tasks[cmd.Index];
    //                 if (task.Status != TaskStatus.Running) continue;
    //
    //                 task.Status = canCastSpell ? TaskStatus.Success : TaskStatus.Failure;
    //                 tasks[cmd.Index] = task;
    //
    //                 EnigmaLogger.Log($"Entity {entity.Index} - CanCastSpellConditional: {(canCastSpell ? "Yes" : "No")}");
    //             }
    //         }
    //
    //         queue.Dispose();
    //     }
    // }

    [DisableAutoCreation]
    public partial class CanCastSpellSystem
        : TaskProcessorSystem<CanCastSpellComponent, CanCastSpellTag>
    {
        protected override TaskStatus Execute(Entity _, UnitBrain brain) => brain.CanCastSpell() ? TaskStatus.Success : TaskStatus.Failure;
    }
}