using Unity.Entities;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;

namespace OneBitRob.AI
{
    [NodeDescription("Request a spell cast (ECS will decide target/point)")]
    public class CastSpellAction : AbstractTaskAction<CastSpellComponent, CastSpellTag, CastSpellSystem>, IAction
    {
        protected override CastSpellComponent CreateBufferElement(ushort runtimeIndex) => new CastSpellComponent { Index = runtimeIndex };
    }

    public struct CastSpellComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct CastSpellTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class CastSpellSystem : TaskProcessorSystem<CastSpellComponent, CastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            // Must have a spell
            if (!EntityManager.HasComponent<SpellConfig>(e))
                return TaskStatus.Failure;

            // Only request when spell is READY
            if (EntityManager.HasComponent<SpellState>(e))
            {
                var ss = EntityManager.GetComponentData<SpellState>(e);
                if (ss.Ready == 0) return TaskStatus.Failure;
            }

            if (!EntityManager.HasComponent<SpellDecisionRequest>(e))
                EntityManager.AddComponentData(e, new SpellDecisionRequest { HasValue = 1 });
            else
            {
                var req = EntityManager.GetComponentData<SpellDecisionRequest>(e);
                req.HasValue = 1;
                EntityManager.SetComponentData(e, req);
            }

            return TaskStatus.Success;
        }
    }
}