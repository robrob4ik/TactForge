using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public class IsTargetInsideBannerAreaConditional : AbstractTaskAction<IsTargetInsideBannerAreaComponent, IsTargetInsideBannerAreaTag, IsTargetInsideBannerAreaSystem>, IConditional
    {
        protected override IsTargetInsideBannerAreaComponent CreateBufferElement(ushort idx) => new() { Index = idx };
    }

    public struct IsTargetInsideBannerAreaComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct IsTargetInsideBannerAreaTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class IsTargetInsideBannerAreaSystem
      : TaskProcessorSystem<IsTargetInsideBannerAreaComponent, IsTargetInsideBannerAreaTag>
    {
        ComponentLookup<LocalTransform>   _posRO;
        ComponentLookup<BannerAssignment> _asgRO;
        ComponentLookup<Banner>           _bannerRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO   = GetComponentLookup<LocalTransform>(true);
            _asgRO   = GetComponentLookup<BannerAssignment>(true);
            _bannerRO= GetComponentLookup<Banner>(true);
        }
        protected override void OnUpdate() { _posRO.Update(this); _asgRO.Update(this); _bannerRO.Update(this); base.OnUpdate(); }

        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<Target>(e)) return TaskStatus.Failure;

            var target = em.GetComponentData<Target>(e).Value;
            if (target == Entity.Null || !_posRO.HasComponent(target)) return TaskStatus.Failure;
            if (!_asgRO.HasComponent(e)) return TaskStatus.Success;

            var asg = _asgRO[e];

            // POKE: keep chasing no matter what
            if (asg.Strategy == BannerStrategy.Poke) return TaskStatus.Success;

            // DEFEND: enforce circle
            if (asg.Banner == Entity.Null || !_bannerRO.HasComponent(asg.Banner)) return TaskStatus.Failure;

            var b       = _bannerRO[asg.Banner];
            float3 basePos = _posRO.HasComponent(asg.Banner) ? _posRO[asg.Banner].Position : b.Position;

            float  r2   = math.max(0f, b.DefendRadius);
            r2 *= r2;

            var tpos = _posRO[target].Position;
            bool inside = math.distancesq(tpos, basePos) <= r2;

            if (inside) return TaskStatus.Success;

            // outside -> clear
            em.SetComponentData(e, new Target { Value = Entity.Null });
            return TaskStatus.Failure;
        }
    }
}
