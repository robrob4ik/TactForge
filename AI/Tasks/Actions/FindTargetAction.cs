using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using static OneBitRob.ECS.SpatialHashComponents;

namespace OneBitRob.AI
{
    [NodeDescription("Finds a valid target via UnitBrain strategy")]
    public class FindTargetAction : AbstractTaskAction<FindTargetComponent, FindTargetTag, FindTargetSystem>, IAction
    {
        protected override FindTargetComponent CreateBufferElement(ushort runtimeIndex) { return new FindTargetComponent { Index = runtimeIndex }; }
    }

    public struct FindTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct FindTargetTag : IComponentData, IEnableableComponent
    {
    }


    
    [DisableAutoCreation]
    public partial class FindTargetSystem
        : TaskProcessorSystem<FindTargetComponent, FindTargetTag>
    {
   
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            // ── Fresh, frame‑local look‑ups (safe even after structural change)
            var posLookup  = GetComponentLookup<LocalTransform>(true);
            var factLookup = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);

            var wanted = new FixedList128Bytes<byte>();
            wanted.Add(brain.UnitDefinition.isEnemy
                ? GameConstants.ALLY_FACTION
                : GameConstants.ENEMY_FACTION);

            brain.CurrentTarget = brain.TargetingStrategy.GetTarget(
                brain.transform.position, 100f, wanted,
                ref posLookup, ref factLookup);

            return brain.CurrentTarget ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
    
}