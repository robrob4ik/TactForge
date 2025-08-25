// FILE: Assets/PROJECT/Scripts/Runtime/AI/Brain/MoveToTargetAction.cs

using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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

    public struct MoveToTargetTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellWindupAndFireSystem))] // ensure casting status is visible
    public partial class MoveToTargetSystem : TaskProcessorSystem<MoveToTargetComponent, MoveToTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

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

            _ecb = new EntityCommandBuffer(Allocator.Temp);
            base.OnUpdate();
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
                    var here = SystemAPI.GetComponent<LocalTransform>(e).Position;
                    var ddLock = EntityManager.GetComponentData<DesiredDestination>(e);
                    ddLock.Position = here;
                    ddLock.HasValue = 1;
                    EntityManager.SetComponentData(e, ddLock);
                    return TaskStatus.Running;
                }
            }
            // ─────────────────────────────────────────────

            // No target → stop and fail
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

            // Opportunistic retargeting (kept from your logic) …
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

            // No-progress fallback retained …
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

            // Push current target position as DesiredDestination (consumed by bridge)
            var targetPos = _posRO[target].Position;
            var dd = EntityManager.GetComponentData<DesiredDestination>(e);
            dd.Position = targetPos;
            dd.HasValue = 1;
            EntityManager.SetComponentData(e, dd);

            // Arrival check
            var self = _posRO[e].Position;
            float stopDist = brain.UnitDefinition.stoppingDistance;
            if (math.distancesq(self, targetPos) <= stopDist * stopDist)
            {
                dd.Position = self;
                dd.HasValue = 1;
                EntityManager.SetComponentData(e, dd);
                return TaskStatus.Success;
            }

            // ─────────────────────────────────────────────────────────────
            // NEW: PERIODIC YIELD to force BT re-evaluation (casting, etc.)
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

                    // Keep current destination but give the BT a chance to run other branches.
                    return TaskStatus.Failure;
                }
            }
            // ─────────────────────────────────────────────────────────────

            return TaskStatus.Running;
        }
    }
}
