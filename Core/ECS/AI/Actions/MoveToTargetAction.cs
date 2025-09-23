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
        ComponentLookup<LocalTransform>     _posRO;
        ComponentLookup<SpatialHashTarget>  _factRO;
        ComponentLookup<InAttackRange>      _inRangeRO;
        ComponentLookup<MovementLock>       _lockRO;
        ComponentLookup<AttackWindup>       _windRO;
        ComponentLookup<UnitStatic>         _unitStaticRO;
        ComponentLookup<UnitRuntimeStats>   _statsRO;

        // Strategy-aware leash (DEFEND only):
        ComponentLookup<BannerAssignment>   _asgRO;
        ComponentLookup<Banner>             _bannerRO;

        private EntityCommandBuffer _ecb;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO       = GetComponentLookup<LocalTransform>(true);
            _factRO      = GetComponentLookup<SpatialHashTarget>(true);
            _inRangeRO   = GetComponentLookup<InAttackRange>(true);
            _lockRO      = GetComponentLookup<MovementLock>(true);
            _windRO      = GetComponentLookup<AttackWindup>(true);
            _unitStaticRO= GetComponentLookup<UnitStatic>(true);
            _statsRO     = GetComponentLookup<UnitRuntimeStats>(true);

            _asgRO       = GetComponentLookup<BannerAssignment>(true);
            _bannerRO    = GetComponentLookup<Banner>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            _inRangeRO.Update(this);
            _lockRO.Update(this);
            _windRO.Update(this);
            _unitStaticRO.Update(this);
            _statsRO.Update(this);

            _asgRO.Update(this);
            _bannerRO.Update(this);

            _ecb = new EntityCommandBuffer(Allocator.Temp);
            base.OnUpdate();
            _ecb.Playback(EntityManager);
            _ecb.Dispose();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            double now = SystemAPI.Time.ElapsedTime;

            if (IsBlocked(e)) { ParkAtSelf(e); return TaskStatus.Running; }
            if (IsInRange(e)) { ParkAtSelf(e); return TaskStatus.Success; }

            if (!TryGetValidTarget(e, out var target))
            {
                ParkAtSelf(e);
                return TaskStatus.Failure;
            }

            float3 selfPos   = _posRO[e].Position;
            float3 targetPos = _posRO[target].Position;

            // Leash ONLY for DEFEND
            if (IsDefendAndOutsideArea(e, selfPos, targetPos))
            {
                SystemAPI.SetComponent(e, new Target { Value = Entity.Null });
                ParkAtSelf(e);
                return TaskStatus.Failure;
            }

            // Chase target: destination = target position
            SetDesiredDestination(e, targetPos);

            // Stop at effective attack range
            float stopAtRange = ComputeEffectiveAttackRange(e, brain);
            EntityManager.SetOrAdd(e, new DesiredStoppingDistance { Value = stopAtRange, HasValue = 1 });

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

        private float ComputeEffectiveAttackRange(Entity e, UnitBrain brain)
        {
            float baseRange = 1.5f; bool isRanged = false;

            if (_unitStaticRO.HasComponent(e))
            {
                var us = _unitStaticRO[e];
                baseRange = math.max(0.01f, us.AttackRangeBase);
                isRanged  = (us.CombatStyle == 2);
            }
            else if (brain.UnitDefinition?.weapon != null)
            {
                baseRange = math.max(0.01f, brain.UnitDefinition.weapon.attackRange);
                isRanged  = brain.UnitDefinition.weapon is RangedWeaponDefinition;
            }

            var stats = _statsRO.HasComponent(e) ? _statsRO[e] : UnitRuntimeStats.Defaults;
            float mult = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;

            return baseRange * math.max(0.0001f, mult);
        }

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

        // ---------- DEFEND leash only ----------
        private bool IsDefendAndOutsideArea(Entity e, float3 selfPos, float3 targetPos)
        {
            if (!_asgRO.HasComponent(e)) return false;
            var asg = _asgRO[e];
            if (asg.Strategy != BannerStrategy.Defend) return false; // POKE: unlimited chase

            if (asg.Banner == Entity.Null || !_bannerRO.HasComponent(asg.Banner)) return false;
            var b = _bannerRO[asg.Banner];
            float3 basePos = _posRO.HasComponent(asg.Banner) ? _posRO[asg.Banner].Position : b.Position;

            float r  = math.max(0f, b.DefendRadius);
            float r2 = r * r;

            return math.distancesq(targetPos, basePos) > r2 || math.distancesq(selfPos, basePos) > r2;
        }
    }
}
