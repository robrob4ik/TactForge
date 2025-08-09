using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Basic setup of EnigmaEngine character")]
    public class SetupUnitAction : AbstractTaskAction<SetupUnitComponent, SetupUnitTag, SetupUnitSystem>, IAction
    {
        protected override SetupUnitComponent CreateBufferElement(ushort runtimeIndex) { return new SetupUnitComponent { Index = runtimeIndex }; }
    }

    public struct SetupUnitComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct SetupUnitTag : IComponentData, IEnableableComponent
    {
    }
    
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class SetupUnitSystem
        : TaskProcessorSystem<SetupUnitComponent, SetupUnitTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            brain.Setup();
            return TaskStatus.Success;             
        }
    }
}