using OneBitRob.Constants;
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Setting destination to Agents Navigation to move agent")]
    public class MoveToTargetAction : AbstractTaskAction<MoveToTargetComponent, MoveToTargetTag, MoveToTargetSystem>, IAction
    {
        protected override MoveToTargetComponent CreateBufferElement(ushort runtimeIndex) { return new MoveToTargetComponent { Index = runtimeIndex }; }
    }

    public struct MoveToTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct MoveToTargetTag : IComponentData, IEnableableComponent
    {
    }

    [DisableAutoCreation]
    public partial class MoveToTargetSystem
        : TaskProcessorSystem<MoveToTargetComponent, MoveToTargetTag>
    {
        /* ── CONFIG – tweak in the Inspector if you like ──────────────────── */
        const int  kFramesBetweenRetarget = 10;          // run re‑target every N frames
        const float kDetectRangeDefault   = 100f;        // fallback when SO value is zero
        
        /* ── cached, read‑only look‑ups – refreshed once per frame ────────── */
        ComponentLookup<LocalTransform>                      _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO = GetComponentLookup<LocalTransform>(true);
            _factRO = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            _factRO.Update(this);
            base.OnUpdate();               // calls Execute() for each task
        }
        
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            if (brain.CurrentTarget == null) return TaskStatus.Failure;

            // ── 1) Retarget (cheap) every k frames ─────────────────────────
            if (UnityEngine.Time.frameCount % kFramesBetweenRetarget == 0 &&
                brain.RemainingDistance() >= brain.UnitDefinition.autoTargetMinSwitchDistance)
            {
                FixedList128Bytes<byte> wanted = default;
                wanted.Add(brain.UnitDefinition.isEnemy
                    ? GameConstants.ALLY_FACTION
                    : GameConstants.ENEMY_FACTION);

                float  range = brain.UnitDefinition.autoTargetDetectionRange;
                if (range <= 0f) range = kDetectRangeDefault;

                /* Returns *GameObject* to stay compatible with your current
                   MonoBehaviour pipeline. No allocations, no safety checks,
                   no Transform sync – the look‑ups are already cached. */
                var candidate = brain.TargetingStrategy.GetTarget(
                    (float3)brain.transform.position, range, wanted,
                    ref _posRO, ref _factRO);

                if (candidate && candidate != brain.CurrentTarget)
                {
                    float3 selfPos  = _posRO[e].Position;
                    float3 candPos  = _posRO[UnitBrainRegistry.GetEntity(candidate)].Position;
                    float  distSqr  = math.distancesq(candPos, selfPos);

                    if (distSqr < brain.RemainingDistance() * brain.RemainingDistance())
                    {
                        Debug.Log($"Switching target from {brain.CurrentTarget.name} to {candidate.name}");
                        brain.CurrentTarget = candidate; // switch!
                    }
                }
            }

            // ‑‑ Reached destination?
            if (brain.HasReachedDestination())
            {
                brain.MoveToPosition(brain.transform.position); // stop
                return TaskStatus.Success;
            }

            // ‑‑ Keep path updated
            var targetPos = brain.CurrentTarget.transform.position;
            if ((targetPos - brain.CurrentTargetPosition).sqrMagnitude >
                AIConstants.MOVED_DIST_SQR_THRESHOLD) { brain.MoveToPosition(targetPos); }

            return TaskStatus.Running;
        }
    }
}