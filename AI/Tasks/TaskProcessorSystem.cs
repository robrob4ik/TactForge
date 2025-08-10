// FILE: OneBitRob/AI/TaskProcessorSystem.cs
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.AI
{
    public interface ITaskCommand { ushort Index { get; set; } }

    public abstract partial class TaskProcessorSystem<TCmd, TTag> : SystemBase
        where TCmd : unmanaged, IBufferElementData, ITaskCommand
        where TTag : unmanaged, IComponentData, IEnableableComponent
    {
        EntityQuery _query;

        protected override void OnCreate()
        {
            _query = GetEntityQuery(
                ComponentType.ReadOnly<TTag>(),
                ComponentType.ReadWrite<TaskComponent>(),
                ComponentType.ReadWrite<TCmd>());
        }

        protected override void OnUpdate()
        {
            var queue  = new NativeQueue<Entity>(Allocator.TempJob);
            var writer = queue.AsParallelWriter();

            var cmdHandle    = GetBufferTypeHandle<TCmd>(true);           // read-only
            var taskHandle   = GetBufferTypeHandle<TaskComponent>(false); // read-write
            var entityHandle = GetEntityTypeHandle();                     // read-only
            var tagHandle    = GetComponentTypeHandle<TTag>(true);        // read-only (presence/enabled)

            Dependency = new CollectRunningJob<TCmd, TTag>
            {
                CmdHandle    = cmdHandle,
                TaskHandle   = taskHandle,
                EntityHandle = entityHandle,
                TagHandle    = tagHandle,
                QueueWriter  = writer
            }.ScheduleParallel(_query, Dependency);

            // Managed section after collection
            Dependency.Complete();

            var em = EntityManager;
            while (queue.TryDequeue(out var e))
            {
                var brain = UnitBrainRegistry.Get(e);
                if (brain == null) continue;

                var tasks = em.GetBuffer<TaskComponent>(e);
                var cmds  = em.GetBuffer<TCmd>(e);

                foreach (var cmd in cmds)
                {
                    ref var task = ref tasks.ElementAt(cmd.Index);
                    if (task.Status != TaskStatus.Running) continue;

#if UNITY_EDITOR
                    brain.CurrentTaskName = typeof(TCmd).Name.Replace("Component", string.Empty);
#endif
                    task.Status = Execute(e, brain);
                }
            }

            queue.Dispose();
        }

        protected abstract TaskStatus Execute(Entity entity, UnitBrain brain);
    }
}
