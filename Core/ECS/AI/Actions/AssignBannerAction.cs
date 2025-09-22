using Unity.Entities;
using Unity.Transforms;
using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Collections;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.AI
{
    public sealed class AssignBannerAction : AbstractTaskAction<AssignBannerComponent, AssignBannerTag, AssignBannerSystem>, IAction
    {
        protected override AssignBannerComponent CreateBufferElement(ushort runtimeIndex) => new AssignBannerComponent { Index = runtimeIndex };
    }

    public struct AssignBannerComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct AssignBannerTag : IComponentData, IEnableableComponent
    {
    }

    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class AssignBannerSystem : TaskProcessorSystem<AssignBannerComponent, AssignBannerTag>
    {
        ComponentLookup<LocalTransform> _ltwRO;
        EntityQuery _bannerQ;

        protected override void OnCreate()
        {
            base.OnCreate();
            _ltwRO = GetComponentLookup<LocalTransform>(true);
            _bannerQ = GetEntityQuery(ComponentType.ReadOnly<Banner>());
        }

        protected override void OnUpdate()
        {
            _ltwRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;

            if (_bannerQ.CalculateEntityCount() == 0) return TaskStatus.Failure;
            if (em.HasComponent<BannerAssignment>(e)) return TaskStatus.Success;

            byte faction = GameConstants.ALLY_FACTION;
            if (em.HasComponent<UnitStatic>(e))
                faction = (em.GetComponentData<UnitStatic>(e).IsEnemy != 0)
                    ? GameConstants.ENEMY_FACTION
                    : GameConstants.ALLY_FACTION;
            else if (em.HasComponent<SpatialHashTarget>(e)) faction = em.GetComponentData<SpatialHashTarget>(e).Faction;

            var banners = _bannerQ.ToEntityArray(Allocator.Temp);
            var bData = _bannerQ.ToComponentDataArray<Banner>(Allocator.Temp);
            float3 selfP = _ltwRO.HasComponent(e) ? _ltwRO[e].Position : float3.zero;

            Entity best = Entity.Null;
            Banner bestB = default;
            float bestD2 = float.MaxValue;

            for (int i = 0; i < banners.Length; i++)
            {
                var b = bData[i];
                if (b.Faction != faction) continue;
                float3 p = _ltwRO.HasComponent(banners[i]) ? _ltwRO[banners[i]].Position : b.Position;
                float d2 = distancesq(selfP, p);
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    best = banners[i];
                    bestB = b;
                }
            }

            banners.Dispose();
            bData.Dispose();
            if (best == Entity.Null) return TaskStatus.Failure;

            float seed = (e.Index ^ (e.Version << 8)) * 0.0001220703125f;
            float ang = seed * 6.2831853f;
            float r = 0.35f;
            float3 offset = new float3(cos(ang) * r, 0f, sin(ang) * r);

            em.AddComponentData(
                e, new BannerAssignment
                {
                    Banner = best,
                    Strategy = bestB.Strategy,
                    HomeOffset = offset
                }
            );
            return TaskStatus.Success;
        }
    }
}