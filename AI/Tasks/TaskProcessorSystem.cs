using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Components;

namespace OneBitRob.AI
{
    public interface ITaskCommand
    {
        ushort Index { get; set; }
    }

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
            var queue = new NativeQueue<Entity>(Allocator.TempJob);
            var writer = queue.AsParallelWriter();

            Dependency = new CollectRunningJob<TCmd>
            {
                CmdHandle = GetBufferTypeHandle<TCmd>(false),
                TaskHandle = GetBufferTypeHandle<TaskComponent>(false),
                EntityHandle = GetEntityTypeHandle(),
                QueueWriter = writer
            }.ScheduleParallel(_query, Dependency);

            Dependency.Complete();

            // ── Step 2 – managed phase ──────────────────────────────────────
            var em = EntityManager;
            while (queue.TryDequeue(out var e))
            {
                if (UnitBrainRegistry.Get(e) is not { } brain) continue;

                var tasks = em.GetBuffer<TaskComponent>(e);
                var cmds = em.GetBuffer<TCmd>(e);

                foreach (var cmd in cmds)
                {
                    ref var task = ref tasks.ElementAt(cmd.Index);
                    if (task.Status != TaskStatus.Running) continue;

                    brain.CurrentTaskName = typeof(TCmd).Name.Replace("Component", string.Empty);
                    
                    task.Status = Execute(e, brain);
                }
            }

            queue.Dispose();
        }

        protected abstract TaskStatus Execute(Entity entity, UnitBrain brain);

        // ────────────────────────────────────────────────────────────────────
        // Step 3 – Burst job
        // ────────────────────────────────────────────────────────────────────
        [BurstCompile]
        struct CollectRunningJob<T> : IJobChunk
            where T : unmanaged, IBufferElementData, ITaskCommand
        {
            public BufferTypeHandle<T> CmdHandle;
            public BufferTypeHandle<TaskComponent> TaskHandle;
            public EntityTypeHandle EntityHandle;
            public NativeQueue<Entity>.ParallelWriter QueueWriter;

            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var cmdBufs = chunk.GetBufferAccessor(ref CmdHandle);
                var taskBufs = chunk.GetBufferAccessor(ref TaskHandle);
                var ents = chunk.GetNativeArray(EntityHandle);

                for (int i = 0; i < chunk.Count; ++i)
                {
                    if (useEnabledMask && !IsBitEnabled(chunkEnabledMask, i)) continue;

                    var cmds = cmdBufs[i];
                    var tasks = taskBufs[i];

                    bool enqueued = false;

                    for (int c = 0; c < cmds.Length; ++c)
                    {
                        var idx = cmds[c].Index;
                        var task = tasks[idx];

                        if (task.Status == TaskStatus.Queued)
                        {
                            task.Status = TaskStatus.Running;
                            tasks[idx] = task;
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
                // v128 exposes two ulongs: ULong0 (bits 0‑63) and ULong1 (bits 64‑127)
                ulong word = index < 64 ? mask.ULong0 : mask.ULong1;
                int bit = index & 63;
                return ((word >> bit) & 1UL) != 0;
            }
        }
    }
}