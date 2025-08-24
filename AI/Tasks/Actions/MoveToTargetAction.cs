// FILE: OneBitRob/AI/Tasks/Actions/MoveToTargetAction.cs
// Change: Avoid invalidating _posRO by deferring AddComponent (RetargetCooldown) with an EntityCommandBuffer.

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

    public struct MoveToTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct MoveToTargetTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellExecutionSystem))] // ← ensure casting status is visible
    public partial class MoveToTargetSystem : TaskProcessorSystem<MoveToTargetComponent, MoveToTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

        // NEW: defer structural changes to end-of-system
        private EntityCommandBuffer _ecb;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO  = GetComponentLookup<LocalTransform>(true);
            _factRO = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);

            // begin ECB for this frame
            _ecb = new EntityCommandBuffer(Allocator.Temp);

            base.OnUpdate();

            // apply deferred structural changes after all Execute calls
            _ecb.Playback(EntityManager);
            _ecb.Dispose();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            float dt = (float)SystemAPI.Time.DeltaTime;

            // ─────────────────────────────────────────────
            // MOVEMENT LOCK WHILE CASTING
            if (EntityManager.HasComponent<SpellWindup>(e))
            {
                var sw = EntityManager.GetComponentData<SpellWindup>(e);
                if (sw.Active != 0)
                {
                    // Park the unit at its current spot
                    var here = SystemAPI.GetComponent<LocalTransform>(e).Position;
                    var ddLock = EntityManager.GetComponentData<DesiredDestination>(e);
                    ddLock.Position = here;
                    ddLock.HasValue = 1;
                    EntityManager.SetComponentData(e, ddLock);

                    // Keep the BT node "alive" without progressing movement
                    return TaskStatus.Running;
                }
            }
            // ─────────────────────────────────────────────

            // No Target → stop and fail
            if (!EntityManager.HasComponent<Target>(e))
            {
                var dd0 = EntityManager.GetComponentData<DesiredDestination>(e);
                dd0.Position = SystemAPI.GetComponent<LocalTransform>(e).Position;
                dd0.HasValue = 1;
                EntityManager.SetComponentData(e, dd0);
                return TaskStatus.Failure;
            }

            var target = EntityManager.GetComponentData<Target>(e).Value;

            // Target entity invalid → clear, stop, and fail
            if (target == Entity.Null || !_posRO.HasComponent(target))
            {
                EntityManager.SetComponentData(e, new Target { Value = Entity.Null });

                var dd1 = EntityManager.GetComponentData<DesiredDestination>(e);
                dd1.Position = SystemAPI.GetComponent<LocalTransform>(e).Position;
                dd1.HasValue = 1;
                EntityManager.SetComponentData(e, dd1);

                return TaskStatus.Failure;
            }

            // Opportunistic retargeting — THROTTLED
            double now = SystemAPI.Time.ElapsedTime;
            float switchInterval = math.max(0f, brain.UnitDefinition.retargetCheckInterval);

            bool canCheck = true;
            if (switchInterval > 0f)
            {
                // SAFE: read if exists, otherwise defer the Add via ECB (no structural change mid-execute)
                RetargetCooldown cd;
                bool hadCd = EntityManager.HasComponent<RetargetCooldown>(e);
                cd = hadCd ? EntityManager.GetComponentData<RetargetCooldown>(e)
                           : new RetargetCooldown { NextTime = 0 };

                canCheck = now >= cd.NextTime;
                if (canCheck)
                {
                    cd.NextTime = now + switchInterval;
                    if (hadCd) EntityManager.SetComponentData(e, cd);
                    else       _ecb.AddComponent(e, cd); // ← defer AddComponent (structural) to end of system
                }
            }

            float3 selfPos = _posRO[e].Position;
            float3 currPos = _posRO[target].Position;
            float stop = brain.UnitDefinition.stoppingDistance;
            float currDistSq = math.distancesq(selfPos, currPos);

            // No-progress fallback: if we haven't closed the distance window for some time, force retarget
            const float progressEpsilon = 0.05f; // ~7cm improvement threshold
            const float stuckTime = 1.5f; // seconds without progress

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
                    // hard retarget ignoring hysteresis
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

                        // reset assist timers upon switching
                        ra.NoProgressTime = 0f;
                        ra.LastDistSq = float.MaxValue;
                        EntityManager.SetComponentData(e, ra);
                    }
                }
            }

            // Opportunistic check if not already forced by no-progress
            if (canCheck)
            {
                var wanted = default(FixedList128Bytes<byte>);
                wanted.Add(brain.UnitDefinition.isEnemy ? GameConstants.ALLY_FACTION : GameConstants.ENEMY_FACTION);

                float range = brain.UnitDefinition.targetDetectionRange > 0
                    ? brain.UnitDefinition.targetDetectionRange
                    : 100f;

                // Early-out: if current target is already very close, retargeting won’t help much.
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

            // Push current target position as DesiredDestination (consumed by bridge)
            var targetPos = _posRO[target].Position;
            var dd = EntityManager.GetComponentData<DesiredDestination>(e);
            dd.Position = targetPos;
            dd.HasValue = 1;
            EntityManager.SetComponentData(e, dd);

            // ECS distance check for completion
            var self = _posRO[e].Position;
            float stopDist = brain.UnitDefinition.stoppingDistance;
            if (math.distancesq(self, targetPos) <= stopDist * stopDist)
            {
                dd.Position = self;
                dd.HasValue = 1;
                EntityManager.SetComponentData(e, dd);
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }
    }
}
