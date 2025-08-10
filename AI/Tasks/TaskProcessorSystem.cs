using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Burst;
using Unity.Burst.Intrinsics;
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
            var tagHandle    = GetComponentTypeHandle<TTag>(true);        // read-only (safety read)

            Dependency = new CollectRunningJob<TCmd, TTag>
            {
                CmdHandle    = cmdHandle,
                TaskHandle   = taskHandle,
                EntityHandle = entityHandle,
                TagHandle    = tagHandle,
                QueueWriter  = writer
            }.ScheduleParallel(_query, Dependency);

            // Managed part after collection
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

        [BurstCompile]
        struct CollectRunningJob<TCmdLocal, TTagLocal> : IJobChunk
            where TCmdLocal : unmanaged, IBufferElementData, ITaskCommand
            where TTagLocal : unmanaged, IComponentData, IEnableableComponent
        {
            [ReadOnly] public BufferTypeHandle<TCmdLocal> CmdHandle;     // read
            public BufferTypeHandle<TaskComponent>       TaskHandle;     // write
            [ReadOnly] public EntityTypeHandle           EntityHandle;   // read
            [ReadOnly] public ComponentTypeHandle<TTagLocal> TagHandle;  // register read

            public NativeQueue<Entity>.ParallelWriter QueueWriter;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var cmdBufs  = chunk.GetBufferAccessor(ref CmdHandle);
                var taskBufs = chunk.GetBufferAccessor(ref TaskHandle);
                var ents     = chunk.GetNativeArray(EntityHandle);

                for (int i = 0; i < chunk.Count; ++i)
                {
                    if (useEnabledMask && !IsBitEnabled(chunkEnabledMask, i))
                        continue;

                    var cmds  = cmdBufs[i];
                    var tasks = taskBufs[i];

                    bool enqueued = false;
                    for (int c = 0; c < cmds.Length; ++c)
                    {
                        var idx  = cmds[c].Index;
                        var task = tasks[idx];

                        if (task.Status == TaskStatus.Queued)
                        {
                            task.Status = TaskStatus.Running;
                            tasks[idx]  = task;
                        }
                        else if (task.Status == TaskStatus.Running && !enqueued)
                        {
                            QueueWriter.Enqueue(ents[i]);
                            enqueued = true;
                        }
                    }
                }
            }

            static bool IsBitEnabled(in v128 mask, int index)
            {
                ulong word = index < 64 ? mask.ULong0 : mask.ULong1;
                int bit = index & 63;
                return ((word >> bit) & 1UL) != 0;
            }
        }
    }
}
