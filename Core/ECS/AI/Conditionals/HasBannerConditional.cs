using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Entities;

namespace OneBitRob.AI
{
    public sealed class HasBannerConditional : AbstractTaskAction<HasBannerComponent, HasBannerTag, HasBannerSystem>, IConditional
    {
        protected override HasBannerComponent CreateBufferElement(ushort runtimeIndex) => new HasBannerComponent { Index = runtimeIndex };
    }

    public struct HasBannerComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct HasBannerTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class HasBannerSystem : TaskProcessorSystem<HasBannerComponent, HasBannerTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<CastRequest>(e)) return TaskStatus.Failure;
            return em.HasComponent<BannerAssignment>(e) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
