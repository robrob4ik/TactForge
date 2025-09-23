using OneBitRob.Core;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
[UpdateBefore(typeof(MoveToTargetSystem))]
public partial class MoveToBannerSystem : TaskProcessorSystem<MoveToBannerComponent, MoveToBannerTag>
{
    ComponentLookup<LocalTransform> _ltwRO;
    ComponentLookup<Banner>         _bannerRO;
    ComponentLookup<Target>         _targetRO;
    ComponentLookup<UnitStatic>     _usRO;

    // NEW
    private EntityCommandBuffer _ecb;

    protected override void OnCreate()
    {
        base.OnCreate();
        _ltwRO    = GetComponentLookup<LocalTransform>(true);
        _bannerRO = GetComponentLookup<Banner>(true);
        _targetRO = GetComponentLookup<Target>(true);
        _usRO     = GetComponentLookup<UnitStatic>(true);
    }

    protected override void OnUpdate()
    {
        _ltwRO.Update(this);
        _bannerRO.Update(this);
        _targetRO.Update(this);
        _usRO.Update(this);

        _ecb = new EntityCommandBuffer(Allocator.Temp);
        base.OnUpdate();
        _ecb.Playback(EntityManager);
        _ecb.Dispose();
    }

    protected override TaskStatus Execute(Entity e, UnitBrain _)
    {
        var em = EntityManager;
        if (!em.HasComponent<BannerAssignment>(e)) return TaskStatus.Failure;

        var asg = em.GetComponentData<BannerAssignment>(e);
        if (asg.Banner == Entity.Null || !_bannerRO.HasComponent(asg.Banner)) return TaskStatus.Failure;

        var b = _bannerRO[asg.Banner];
        float3 basePos = _ltwRO.HasComponent(asg.Banner) ? _ltwRO[asg.Banner].Position : b.Position;
        float3 fwd     = math.normalizesafe(b.Forward, new float3(0,0,1));

        float leashR = math.max(0f, asg.Strategy == BannerStrategy.Defend ? b.DefendRadius : (b.PokeAdvance + b.DefendRadius));
        // CHANGED: defer BannerLeash upsert
        _ecb.SetOrAdd(EntityManager, e, new BannerLeash { Radius = leashR, Slack = 1.5f });

        if (_targetRO.HasComponent(e))
        {
            var t = _targetRO[e].Value;
            if (t != Entity.Null && _ltwRO.HasComponent(t)) return TaskStatus.Success;
        }

        float3 home = basePos + asg.HomeOffset;
        if (asg.Strategy == BannerStrategy.Poke)
            home = basePos + fwd * math.max(0f, b.PokeAdvance) + asg.HomeOffset;

        var dd = SystemAPI.GetComponent<DesiredDestination>(e);
        dd.Position = home; dd.HasValue = 1;
        SystemAPI.SetComponent(e, dd);

        float movingStop = _usRO.HasComponent(e) ? math.max(0f, _usRO[e].StoppingDistance) : 0.75f;
        // CHANGED: defer upsert of DesiredStoppingDistance (structural if missing)
        _ecb.SetOrAdd(EntityManager, e, new DesiredStoppingDistance { Value = movingStop, HasValue = 1 });

        return TaskStatus.Success;
    }
}
}
