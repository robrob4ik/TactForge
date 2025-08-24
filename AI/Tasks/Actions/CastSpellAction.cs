// FILE: OneBitRob/AI/CastSpellAction.cs
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Commit a planned spell (writes CastRequest from PlannedCast)")]
    public class CastSpellAction : AbstractTaskAction<CastSpellComponent, CastSpellTag, CastSpellSystem>, IAction
    {
        protected override CastSpellComponent CreateBufferElement(ushort runtimeIndex)
            => new CastSpellComponent { Index = runtimeIndex };
    }

    public struct CastSpellComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct CastSpellTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(RotateToSpellTargetSystem))]
    [UpdateBefore(typeof(SpellExecutionSystem))]
    public partial class CastSpellSystem : TaskProcessorSystem<CastSpellComponent, CastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellConfig>(e)) return TaskStatus.Failure;
            if (!em.HasComponent<SpellState>(e))  return TaskStatus.Failure;

            var ss = em.GetComponentData<SpellState>(e);
            if (ss.Ready == 0) return TaskStatus.Failure;

            if (!em.HasComponent<PlannedCast>(e)) return TaskStatus.Failure;

            var plan = em.GetComponentData<PlannedCast>(e);
            if (plan.HasValue == 0) return TaskStatus.Failure;

            var cr = new CastRequest
            {
                Kind        = plan.Kind,
                Target      = plan.Target,
                AoEPosition = plan.AoEPosition,
                HasValue    = 1
            };

            if (em.HasComponent<CastRequest>(e)) em.SetComponentData(e, cr);
            else                                  em.AddComponentData(e, cr);

            // clear plan
            em.SetComponentData(e, default(PlannedCast));
            return TaskStatus.Success;
        }
    }
}
