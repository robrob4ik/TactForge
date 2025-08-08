using OneBitRob.Constants;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Rotating to spell target")]
    public class RotateToSpellTargetAction : AbstractTaskAction<RotateToSpellTargetComponent, RotateToSpellTargetTag, RotateToSpellTargetSystem>, IAction
    {
        protected override RotateToSpellTargetComponent CreateBufferElement(ushort runtimeIndex) { return new RotateToSpellTargetComponent { Index = runtimeIndex }; }
    }

    public struct RotateToSpellTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct RotateToSpellTargetTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    public partial class RotateToSpellTargetSystem
        : TaskProcessorSystem<RotateToSpellTargetComponent, RotateToSpellTargetTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            if (brain.CurrentTarget == null) return TaskStatus.Failure;

            brain.RotateToSpellTarget();
            EcsLogger.Info(this, $"[{e.Index}] rotate‑to‑spell‑target");
            return TaskStatus.Success;
        }
    }
}