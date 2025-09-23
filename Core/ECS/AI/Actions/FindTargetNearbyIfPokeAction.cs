using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public class FindTargetNearbyIfPokeAction : AbstractTaskAction<FindTargetNearbyIfPokeComponent, FindTargetNearbyIfPokeTag, FindTargetNearbyIfPokeSystem>, IAction
    {
        protected override FindTargetNearbyIfPokeComponent CreateBufferElement(ushort idx) => new() { Index = idx };
    }

    public struct FindTargetNearbyIfPokeComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct FindTargetNearbyIfPokeTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class FindTargetNearbyIfPokeSystem : TaskProcessorSystem<FindTargetNearbyIfPokeComponent, FindTargetNearbyIfPokeTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashTarget> _factRO;
        ComponentLookup<UnitStatic> _usRO;
        ComponentLookup<BannerAssignment> _asgRO;
        ComponentLookup<Banner> _bannerRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO = GetComponentLookup<LocalTransform>(true);
            _factRO = GetComponentLookup<SpatialHashTarget>(true);
            _usRO = GetComponentLookup<UnitStatic>(true);
            _asgRO = GetComponentLookup<BannerAssignment>(true);
            _bannerRO = GetComponentLookup<Banner>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            _usRO.Update(this);
            _asgRO.Update(this);
            _bannerRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var em = EntityManager;
            if (!_asgRO.HasComponent(e)) return TaskStatus.Failure;
            var asg = _asgRO[e];
            if (asg.Strategy != BannerStrategy.Poke) return TaskStatus.Failure;

            if (asg.Banner == Entity.Null || !_bannerRO.HasComponent(asg.Banner)) return TaskStatus.Failure;

            float3 selfPos = _posRO[e].Position;

            // Near corridor? Then skip nearby search and let corridor search run.
            var b = _bannerRO[asg.Banner];
            float3 basePos = _posRO.HasComponent(asg.Banner) ? _posRO[asg.Banner].Position : b.Position;
            float3 fwd = math.normalizesafe(b.Forward, new float3(0, 0, 1));
            float3 A = basePos;
            float3 C = basePos + fwd * math.max(0f, b.PokeAdvance);
            float r = math.max(0f, b.DefendRadius);

            if (InsideCapsule(selfPos, A, C, r + 1.0f)) return TaskStatus.Failure; // allow corridor search to handle

            // Far away: look for nearby target around self
            float range = _usRO.HasComponent(e) ? math.max(0f, _usRO[e].TargetDetectionRange) : 100f;

            var wanted = new Unity.Collections.FixedList128Bytes<byte>();
            wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

            var closest = SpatialHashSearch.GetClosest(selfPos, range, wanted, ref _posRO, ref _factRO);
            if (closest == Entity.Null) return TaskStatus.Failure;

            em.SetComponentData(e, new Target { Value = closest });
            return TaskStatus.Success;
        }

        static bool InsideCapsule(float3 p, float3 a, float3 b, float radius)
        {
            float3 ab = b - a;
            float abLen2 = math.max(1e-6f, math.lengthsq(ab));
            float t = math.saturate(math.dot(p - a, ab) / abLen2);
            float3 closest = a + ab * t;
            return math.distancesq(p, closest) <= radius * radius;
        }
    }
}