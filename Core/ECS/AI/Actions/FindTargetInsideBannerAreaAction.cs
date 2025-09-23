using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    public class FindTargetInsideBannerAreaAction : AbstractTaskAction<FindTargetInsideBannerAreaComponent, FindTargetInsideBannerAreaTag, FindTargetInsideBannerAreaSystem>, IAction
    {
        protected override FindTargetInsideBannerAreaComponent CreateBufferElement(ushort idx) => new() { Index = idx };
    }

    public struct FindTargetInsideBannerAreaComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct FindTargetInsideBannerAreaTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
    public partial class FindTargetInsideBannerAreaSystem
        : TaskProcessorSystem<FindTargetInsideBannerAreaComponent, FindTargetInsideBannerAreaTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashTarget> _factRO;
        ComponentLookup<BannerAssignment> _asgRO;
        ComponentLookup<Banner> _bannerRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO = GetComponentLookup<LocalTransform>(true);
            _factRO = GetComponentLookup<SpatialHashTarget>(true);
            _asgRO = GetComponentLookup<BannerAssignment>(true);
            _bannerRO = GetComponentLookup<Banner>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            _asgRO.Update(this);
            _bannerRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var em = EntityManager;
            if (!_asgRO.HasComponent(e)) return TaskStatus.Failure;

            var asg = _asgRO[e];
            if (asg.Banner == Entity.Null || !_bannerRO.HasComponent(asg.Banner)) return TaskStatus.Failure;

            var b = _bannerRO[asg.Banner];
            float3 basePos = _posRO.HasComponent(asg.Banner) ? _posRO[asg.Banner].Position : b.Position;
            float3 fwd = math.normalizesafe(b.Forward, new float3(0, 0, 1));

            // Hostiles
            var wanted = default(FixedList128Bytes<byte>);
            wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

            Entity best = Entity.Null;
            float bestD2 = float.MaxValue;
            float3 selfPos = _posRO[e].Position;

            if (asg.Strategy == BannerStrategy.Poke)
            {
                // Corridor: only active if the unit is currently near the corridor
                float3 A = basePos;
                float3 C = basePos + fwd * max(0f, b.PokeAdvance);
                float r = max(0f, b.DefendRadius);
                bool selfNear = InsideCapsule(selfPos, A, C, r + 1.0f);
                if (!selfNear) return TaskStatus.Failure; // far away (after chase): skip corridor search

                FindClosestInCapsule(A, C, r, wanted, ref best, ref bestD2, selfPos);
            }
            else // DEFEND: circle
            {
                float radius = max(0f, b.DefendRadius);
                FindClosestInCircle(basePos, radius, wanted, ref best, ref bestD2, selfPos);
            }

            if (best == Entity.Null) return TaskStatus.Failure;
            em.SetComponentData(e, new Target { Value = best });
            return TaskStatus.Success;
        }

        void FindClosestInCircle(float3 center, float radius, FixedList128Bytes<byte> wanted, ref Entity best, ref float bestD2, float3 prefOrigin)
        {
            float cell = SpatialHashBuildSystem.CellSize;
            float3 min = center - new float3(radius);
            float3 max = center + new float3(radius);

            int3 cmin = (int3)floor(new float3(min.x, 0, min.z) / cell);
            int3 cmax = (int3)floor(new float3(max.x, 0, max.z) / cell);

            var grid = SpatialHashBuildSystem.Grid;
            for (int cz = cmin.z; cz <= cmax.z; cz++)
            for (int cx = cmin.x; cx <= cmax.x; cx++)
            {
                int key = (int)hash(new int3(cx, 0, cz));
                if (!grid.TryGetFirstValue(key, out var cand, out var it)) continue;

                do
                {
                    if (!_posRO.HasComponent(cand) || !_factRO.HasComponent(cand)) continue;
                    var fact = _factRO[cand].Faction;
                    if (!Contains(wanted, fact)) continue;

                    float3 p = _posRO[cand].Position;
                    if (distancesq(p, center) > radius * radius) continue;

                    float d2 = distancesq(p, prefOrigin);
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        best = cand;
                    }
                } while (grid.TryGetNextValue(out cand, ref it));
            }
        }

        void FindClosestInCapsule(float3 A, float3 C, float radius, FixedList128Bytes<byte> wanted, ref Entity best, ref float bestD2, float3 prefOrigin)
        {
            float cell = SpatialHashBuildSystem.CellSize;
            float3 min = math.min(A, C) - new float3(radius);
            float3 max = math.max(A, C) + new float3(radius);

            int3 cmin = (int3)floor(new float3(min.x, 0, min.z) / cell);
            int3 cmax = (int3)floor(new float3(max.x, 0, max.z) / cell);

            var grid = SpatialHashBuildSystem.Grid;
            for (int cz = cmin.z; cz <= cmax.z; cz++)
            for (int cx = cmin.x; cx <= cmax.x; cx++)
            {
                int key = (int)hash(new int3(cx, 0, cz));
                if (!grid.TryGetFirstValue(key, out var cand, out var it)) continue;

                do
                {
                    if (!_posRO.HasComponent(cand) || !_factRO.HasComponent(cand)) continue;
                    var fact = _factRO[cand].Faction;
                    if (!Contains(wanted, fact)) continue;

                    float3 p = _posRO[cand].Position;
                    if (!InsideCapsule(p, A, C, radius)) continue;

                    float d2 = distancesq(p, prefOrigin);
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        best = cand;
                    }
                } while (grid.TryGetNextValue(out cand, ref it));
            }
        }

        static bool Contains(FixedList128Bytes<byte> set, byte value)
        {
            for (int i = 0; i < set.Length; i++)
                if (set[i] == value)
                    return true;
            return false;
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