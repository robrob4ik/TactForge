using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
    [NodeDescription("Sets DesiredDestination to current Target position")]
    public class MoveToTargetAction : AbstractTaskAction<MoveToTargetComponent, MoveToTargetTag, MoveToTargetSystem>, IAction
    {
        protected override MoveToTargetComponent CreateBufferElement(ushort runtimeIndex) => new MoveToTargetComponent { Index = runtimeIndex };
    }

    public struct MoveToTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct MoveToTargetTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))]
    public partial class MoveToTargetSystem : TaskProcessorSystem<MoveToTargetComponent, MoveToTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

        // New lookups reused for gating
        ComponentLookup<InAttackRange> _inRangeRO;
        ComponentLookup<MovementLock>  _lockRO;
        ComponentLookup<AttackWindup>  _windRO;

        private EntityCommandBuffer _ecb;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO   = GetComponentLookup<LocalTransform>(true);
            _factRO  = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            _inRangeRO = GetComponentLookup<InAttackRange>(true);
            _lockRO    = GetComponentLookup<MovementLock>(true);
            _windRO    = GetComponentLookup<AttackWindup>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            _inRangeRO.Update(this);
            _lockRO.Update(this);
            _windRO.Update(this);

            _ecb = new EntityCommandBuffer(Allocator.Temp);
            base.OnUpdate();
            _ecb.Playback(EntityManager);
            _ecb.Dispose();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            float dt = (float)SystemAPI.Time.DeltaTime;

            // Lock → park and run
            bool weaponWindup = _windRO.HasComponent(e) && _windRO[e].Active != 0;
            bool anyLock = _lockRO.HasComponent(e) &&
                           (_lockRO[e].Flags & (MovementLockFlags.Casting | MovementLockFlags.Attacking)) != 0;

            if (weaponWindup || anyLock)
            {
                ParkAtSelf(e);
                return TaskStatus.Running;
            }

            // In range → park and succeed
            if (_inRangeRO.HasComponent(e) && _inRangeRO[e].Value != 0)
            {
                ParkAtSelf(e);
                return TaskStatus.Success;
            }

            // Normal chase….
            if (!EntityManager.HasComponent<Target>(e))
            {
                ParkAtSelf(e);
                return TaskStatus.Failure;
            }

            var target = EntityManager.GetComponentData<Target>(e).Value;
            if (target == Entity.Null || !_posRO.HasComponent(target))
            {
                EntityManager.SetComponentData(e, new Target { Value = Entity.Null });
                ParkAtSelf(e);
                return TaskStatus.Failure;
            }
            
            // Opportunistic retargeting (kept) …
            double now = SystemAPI.Time.ElapsedTime;
            float switchInterval = math.max(0f, brain.UnitDefinition.retargetCheckInterval);

            bool canCheck = true;
            if (switchInterval > 0f)
            {
                RetargetCooldown cd;
                bool hadCd = EntityManager.HasComponent<RetargetCooldown>(e);
                cd = hadCd ? EntityManager.GetComponentData<RetargetCooldown>(e)
                           : new RetargetCooldown { NextTime = 0 };

                canCheck = now >= cd.NextTime;
                if (canCheck)
                {
                    cd.NextTime = now + switchInterval;
                    if (hadCd) EntityManager.SetComponentData(e, cd);
                    else       _ecb.AddComponent(e, cd);
                }
            }

            float3 selfPos = _posRO[e].Position;
            float3 currPos = _posRO[target].Position;
            float stop = brain.UnitDefinition.stoppingDistance;
            float currDistSq = math.distancesq(selfPos, currPos);

            // No-progress assist (kept) …
            const float progressEpsilon = 0.05f;
            const float stuckTime = 1.5f;

            if (EntityManager.HasComponent<OneBitRob.ECS.RetargetAssist>(e))
            {
                var ra = EntityManager.GetComponentData<OneBitRob.ECS.RetargetAssist>(e);

                if (ra.LastDistSq <= currDistSq + progressEpsilon)
                    ra.NoProgressTime += dt;
                else
                    ra.NoProgressTime = 0f;

                ra.LastPos = selfPos;
                ra.LastDistSq = currDistSq;
                EntityManager.SetComponentData(e, ra);

                if (ra.NoProgressTime > stuckTime)
                {
                    var wanted = default(FixedList128Bytes<byte>);
                    wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

                    float range = brain.UnitDefinition.targetDetectionRange > 0
                        ? brain.UnitDefinition.targetDetectionRange
                        : 100f;

                    var candidate = SpatialHashSearch.GetClosest(selfPos, range, wanted, ref _posRO, ref _factRO);
                    if (candidate != Entity.Null && candidate != target)
                    {
                        target = candidate;
                        EntityManager.SetComponentData(e, new Target { Value = candidate });
                        ra.NoProgressTime = 0f;
                        ra.LastDistSq = float.MaxValue;
                        EntityManager.SetComponentData(e, ra);
                    }
                }
            }

            if (canCheck)
            {
                var wanted = default(FixedList128Bytes<byte>);
                wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

                float range = brain.UnitDefinition.targetDetectionRange > 0 ? brain.UnitDefinition.targetDetectionRange : 100f;

                if (currDistSq > (stop * stop * 4f))
                {
                    var candidate = SpatialHashSearch.GetClosest(selfPos, range, wanted, ref _posRO, ref _factRO);
                    if (candidate != Entity.Null && candidate != target)
                    {
                        float minSwitch = math.max(0f, brain.UnitDefinition.autoTargetMinSwitchDistance);
                        bool shouldSwitch = true;

                        if (minSwitch > 0f)
                        {
                            float distCand = math.distance(_posRO[candidate].Position, selfPos);
                            float distCurr = math.sqrt(currDistSq);
                            shouldSwitch = (distCurr - distCand) >= minSwitch;
                        }

                        if (shouldSwitch)
                        {
                            target = candidate;
                            EntityManager.SetComponentData(e, new Target { Value = candidate });
                        }
                    }
                }
            }

            // Push desired destination toward target (agent’s StoppingDistance will handle spacing)
            var targetPos = _posRO[target].Position;
            SetDesiredDestination(e, targetPos);

            // Arrival check (stoppingDistance)
            float stopDist = brain.UnitDefinition.stoppingDistance;
            if (math.distancesq(selfPos, targetPos) <= stopDist * stopDist)
            {
                ParkAtSelf(e);
                return TaskStatus.Success;
            }
            
            // Periodic yield (kept)
            float yieldInterval = math.max(0f, brain.UnitDefinition.moveRecheckYieldInterval);
            if (yieldInterval > 0f)
            {
                var had = EntityManager.HasComponent<OneBitRob.ECS.BehaviorYieldCooldown>(e);
                var y = had ? EntityManager.GetComponentData<OneBitRob.ECS.BehaviorYieldCooldown>(e)
                            : new OneBitRob.ECS.BehaviorYieldCooldown { NextTime = 0 };

                if (now >= y.NextTime)
                {
                    y.NextTime = now + yieldInterval;
                    if (had) EntityManager.SetComponentData(e, y);
                    else     _ecb.AddComponent(e, y);

                    return TaskStatus.Failure;
                }
            }

            return TaskStatus.Running;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers (prevent duplicate local variables everywhere)
        // ─────────────────────────────────────────────────────────────────────
        private void ParkAtSelf(Entity e)
        {
            float3 here = SystemAPI.GetComponent<LocalTransform>(e).Position;
            SetDesiredDestination(e, here);
        }

        private void SetDesiredDestination(Entity e, float3 position)
        {
            var dd = EntityManager.GetComponentData<DesiredDestination>(e);
            dd.Position = position;
            dd.HasValue = 1;
            EntityManager.SetComponentData(e, dd);
        }
    }
}
