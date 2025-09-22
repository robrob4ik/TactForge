using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.ECS; 
using Opsive.BehaviorDesigner.Runtime.Tasks;
using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    public sealed class MoveToBannerAction : AbstractTaskAction<MoveToBannerComponent, MoveToBannerTag, MoveToBannerSystem>, IAction
    {
        protected override MoveToBannerComponent CreateBufferElement(ushort runtimeIndex) => new MoveToBannerComponent { Index = runtimeIndex };
    }

    public struct MoveToBannerComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct MoveToBannerTag       : IComponentData, IEnableableComponent { }

    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(MoveToTargetSystem))]
    public partial class MoveToBannerSystem : TaskProcessorSystem<MoveToBannerComponent, MoveToBannerTag>
    {
        ComponentLookup<LocalTransform> _ltwRO;
        ComponentLookup<Banner> _bannerRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _ltwRO    = GetComponentLookup<LocalTransform>(true);
            _bannerRO = GetComponentLookup<Banner>(true);
        }

        protected override void OnUpdate()
        {
            _ltwRO.Update(this);
            _bannerRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<BannerAssignment>(e)) return TaskStatus.Failure;

            var asg = em.GetComponentData<BannerAssignment>(e);
            if (asg.Banner == Entity.Null || !_bannerRO.HasComponent(asg.Banner)) return TaskStatus.Failure;

            var b = _bannerRO[asg.Banner];
            float3 basePos = _ltwRO.HasComponent(asg.Banner) ? _ltwRO[asg.Banner].Position : b.Position;

            float3 fwd = normalize(b.Forward);
            if (!all(isfinite(fwd))) fwd = new float3(0, 0, 1);

            float3 home = basePos + asg.HomeOffset;
            if (asg.Strategy == BannerStrategy.Poke)
                home = basePos + fwd * max(0f, b.PokeAdvance) + asg.HomeOffset;

            var dd = SystemAPI.GetComponent<DesiredDestination>(e);
            dd.Position = home; dd.HasValue = 1;
            SystemAPI.SetComponent(e, dd);

            return TaskStatus.Success;
        }
    }
}
