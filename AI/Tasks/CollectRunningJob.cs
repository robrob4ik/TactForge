using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace OneBitRob.AI
{
    [BurstCompile]
    public struct CollectRunningJob<TCmd, TTag> : IJobChunk
        where TCmd : unmanaged, IBufferElementData, ITaskCommand
        where TTag : unmanaged, IComponentData, IEnableableComponent
    {
        [ReadOnly]
        public BufferTypeHandle<TCmd> CmdHandle;

        public BufferTypeHandle<TaskComponent> TaskHandle;

        [ReadOnly]
        public EntityTypeHandle EntityHandle;

        [ReadOnly]
        public ComponentTypeHandle<TTag> TagHandle;

        public NativeQueue<Entity>.ParallelWriter QueueWriter;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
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
            ulong word = index < 64 ? mask.ULong0 : mask.ULong1;
            int bit = index & 63;
            return ((word >> bit) & 1UL) != 0;
        }
    }
}