using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    public class MoveToTargetAction : AbstractTaskAction<MoveToTargetComponent, MoveToTargetTag, MoveToTargetSystem>, IAction
    {
        protected override MoveToTargetComponent CreateBufferElement(ushort runtimeIndex) => new MoveToTargetComponent { Index = runtimeIndex };
    }

    public struct MoveToTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct MoveToTargetTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class MoveToTargetSystem : TaskProcessorSystem<MoveToTargetComponent, MoveToTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashTarget> _factRO;
        ComponentLookup<InAttackRange> _inRangeRO;
        ComponentLookup<MovementLock>  _lockRO;
        ComponentLookup<AttackWindup>  _windRO;
        ComponentLookup<UnitStatic>    _unitStaticRO;

        private EntityCommandBuffer _ecb;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO   = GetComponentLookup<LocalTransform>(true);
            _factRO  = GetComponentLookup<SpatialHashTarget>(true);
            _inRangeRO = GetComponentLookup<InAttackRange>(true);
            _lockRO    = GetComponentLookup<MovementLock>(true);
            _windRO    = GetComponentLookup<AttackWindup>(true);
            _unitStaticRO = GetComponentLookup<UnitStatic>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            _inRangeRO.Update(this);
            _lockRO.Update(this);
            _windRO.Update(this);
            _unitStaticRO.Update(this);

            _ecb = new EntityCommandBuffer(Allocator.Temp);
            base.OnUpdate();
            _ecb.Playback(EntityManager);
            _ecb.Dispose();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            float dt  = (float)SystemAPI.Time.DeltaTime;
            double now= SystemAPI.Time.ElapsedTime;

            if (IsBlocked(e)) { ParkAtSelf(e); return TaskStatus.Running; }
            if (IsInRange(e)) { ParkAtSelf(e); return TaskStatus.Success; }

            if (!TryGetValidTarget(e, out var target))
            {
                ParkAtSelf(e);
                return TaskStatus.Failure;
            }

            float3 selfPos   = _posRO[e].Position;
            float3 targetPos = _posRO[target].Position;

            UpdateRetargetAssist(e, brain, dt, selfPos, target, ref targetPos);
            MaybeRetarget(e, brain, now, selfPos, ref target, ref targetPos);
            SetDesiredDestination(e, targetPos);

            float stopDist = ReadStoppingDistance(e, brain);
            if (Arrived(selfPos, targetPos, stopDist))
            { ParkAtSelf(e); return TaskStatus.Success; }

            if (ShouldYield(e, now, brain))
                return TaskStatus.Failure;

            return TaskStatus.Running;
        }

        private bool IsBlocked(Entity e)
        {
            bool weaponWindup = _windRO.HasComponent(e) && _windRO[e].Active != 0;
            bool anyLock = _lockRO.HasComponent(e) && (_lockRO[e].Flags & (MovementLockFlags.Casting | MovementLockFlags.Attacking)) != 0;
            return weaponWindup || anyLock;
        }

        private bool IsInRange(Entity e) => _inRangeRO.HasComponent(e) && _inRangeRO[e].Value != 0;

        private bool TryGetValidTarget(Entity e, out Entity target)
        {
            target = SystemAPI.HasComponent<Target>(e) ? SystemAPI.GetComponent<Target>(e).Value : Entity.Null;
            if (target == Entity.Null || !_posRO.HasComponent(target))
            {
                if (SystemAPI.HasComponent<Target>(e))
                    SystemAPI.SetComponent(e, new Target { Value = Entity.Null });
                return false;
            }
            return true;
        }

        private void MaybeRetarget(Entity e, UnitBrain brain, double now, float3 selfPos, ref Entity target, ref float3 targetPos)
        {
            var r = ReadRetarget(e, brain);

            bool canCheck = r.RetargetCheckInterval <= 0f;
            if (!canCheck)
            {
                var had = SystemAPI.HasComponent<RetargetCooldown>(e);
                var cd  = had ? SystemAPI.GetComponent<RetargetCooldown>(e) : new RetargetCooldown { NextTime = 0 };
                canCheck = now >= cd.NextTime;
                if (canCheck)
                {
                    cd.NextTime = now + r.RetargetCheckInterval;
                    if (had) SystemAPI.SetComponent(e, cd); else _ecb.AddComponent(e, cd);
                }
            }
            if (!canCheck) return;

            var wanted = default(FixedList128Bytes<byte>);
            wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

            float range = ReadTargetDetectionRange(e, brain);
            float stop  = ReadStoppingDistance(e, brain);
            float currDistSq = math.distancesq(selfPos, targetPos);

            if (currDistSq > (stop * stop * 4f))
            {
                var candidate = SpatialHashSearch.GetClosest(selfPos, range, wanted, ref _posRO, ref _factRO);
                if (candidate != Entity.Null && candidate != target)
                {
                    float minSwitch = math.max(0f, r.AutoSwitchMinDistance);
                    bool  shouldSwitch = true;

                    if (minSwitch > 0f)
                    {
                        float distCand = math.distance(_posRO[candidate].Position, selfPos);
                        float distCurr = math.sqrt(currDistSq);
                        shouldSwitch   = (distCurr - distCand) >= minSwitch;
                    }

                    if (shouldSwitch)
                    {
                        target    = candidate;
                        targetPos = _posRO[candidate].Position;
                        SystemAPI.SetComponent(e, new Target { Value = candidate });
                    }
                }
            }
        }

        private void UpdateRetargetAssist(Entity e, UnitBrain brain, float dt, float3 selfPos, Entity target, ref float3 targetPos)
        {
            float currDistSq = math.distancesq(selfPos, targetPos);

            if (SystemAPI.HasComponent<RetargetAssist>(e))
            {
                var ra = SystemAPI.GetComponent<RetargetAssist>(e);

                const float progressEpsilon = 0.05f;
                const float stuckTime = 1.5f;

                if (ra.LastDistSq <= currDistSq + progressEpsilon) ra.NoProgressTime += dt;
                else                                               ra.NoProgressTime  = 0f;

                ra.LastPos   = selfPos;
                ra.LastDistSq= currDistSq;
                SystemAPI.SetComponent(e, ra);

                if (ra.NoProgressTime > stuckTime)
                {
                    var wanted = default(FixedList128Bytes<byte>);
                    wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

                    float range = ReadTargetDetectionRange(e, brain);

                    var candidate = SpatialHashSearch.GetClosest(selfPos, range, wanted, ref _posRO, ref _factRO);
                    if (candidate != Entity.Null && candidate != target)
                    {
                        target = candidate;
                        SystemAPI.SetComponent(e, new Target { Value = candidate });
                        ra.NoProgressTime = 0f;
                        ra.LastDistSq     = float.MaxValue;
                        SystemAPI.SetComponent(e, ra);
                        targetPos = _posRO[candidate].Position;
                    }
                }
            }
        }

        private static bool Arrived(float3 selfPos, float3 targetPos, float stopDist) => math.distancesq(selfPos, targetPos) <= stopDist * stopDist;

        private bool ShouldYield(Entity e, double now, UnitBrain brain)
        {
            var r = ReadRetarget(e, brain);
            float yieldInterval = math.max(0f, r.MoveRecheckYieldInterval);
            if (yieldInterval <= 0f) return false;

            var had = SystemAPI.HasComponent<BehaviorYieldCooldown>(e);
            var y   = had ? SystemAPI.GetComponent<BehaviorYieldCooldown>(e) : new BehaviorYieldCooldown { NextTime = 0 };

            if (now >= y.NextTime)
            {
                y.NextTime = now + yieldInterval;
                if (had) SystemAPI.SetComponent(e, y); else _ecb.AddComponent(e, y);
                return true;
            }
            return false;
        }

        private void ParkAtSelf(Entity e)
        {
            float3 here = SystemAPI.GetComponent<LocalTransform>(e).Position;
            SetDesiredDestination(e, here);
        }

        private void SetDesiredDestination(Entity e, float3 position)
        {
            var dd = SystemAPI.GetComponent<DesiredDestination>(e);
            dd.Position = position; dd.HasValue = 1;
            SystemAPI.SetComponent(e, dd);
        }

        private float ReadStoppingDistance(Entity e, UnitBrain brain)
        {
            if (_unitStaticRO.HasComponent(e)) return _unitStaticRO[e].StoppingDistance;
            return brain.UnitDefinition != null ? math.max(0f, brain.UnitDefinition.stoppingDistance) : 1.5f;
        }
        private float ReadTargetDetectionRange(Entity e, UnitBrain brain)
        {
            if (_unitStaticRO.HasComponent(e)) return _unitStaticRO[e].TargetDetectionRange;
            return brain.UnitDefinition != null ? math.max(0f, brain.UnitDefinition.targetDetectionRange) : 100f;
        }
        private RetargetingSettings ReadRetarget(Entity e, UnitBrain brain)
        {
            if (_unitStaticRO.HasComponent(e)) return _unitStaticRO[e].Retarget;
            return new RetargetingSettings
            {
                AutoSwitchMinDistance    = brain.UnitDefinition != null ? math.max(0f, brain.UnitDefinition.autoTargetMinSwitchDistance) : 0f,
                RetargetCheckInterval    = brain.UnitDefinition != null ? math.max(0f, brain.UnitDefinition.retargetCheckInterval)       : 0f,
                MoveRecheckYieldInterval = brain.UnitDefinition != null ? math.max(0f, brain.UnitDefinition.moveRecheckYieldInterval)    : 0f
            };
        }
    }
}
