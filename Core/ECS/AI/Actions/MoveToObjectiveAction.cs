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
   namespace OneBitRob.AI
{
    public sealed class MoveToObjectiveAction : AbstractTaskAction<MoveToObjectiveComponent, MoveToObjectiveTag, MoveToObjectiveSystem>, IAction
    {
        protected override MoveToObjectiveComponent CreateBufferElement(ushort idx) => new() { Index = idx };
    }

    public struct MoveToObjectiveComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct MoveToObjectiveTag       : IComponentData, IEnableableComponent { }

    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(MoveToTargetSystem))]
    public partial class MoveToObjectiveSystem : TaskProcessorSystem<MoveToObjectiveComponent, MoveToObjectiveTag>
    {
        ComponentLookup<LocalTransform> _ltwRO;
        ComponentLookup<UnitStatic>     _usRO;
        EntityQuery                     _baseQ;

        // NEW
        private EntityCommandBuffer _ecb;

        protected override void OnCreate()
        {
            base.OnCreate();
            _ltwRO = GetComponentLookup<LocalTransform>(true);
            _usRO  = GetComponentLookup<UnitStatic>(true);

            // CHANGED: only require PlayerBase now (no LocalTransform on that entity)
            _baseQ = GetEntityQuery(ComponentType.ReadOnly<PlayerBase>());
        }

        protected override void OnUpdate()
        {
            _ltwRO.Update(this);
            _usRO.Update(this);

            // keep structural ops deferred
            _ecb = new EntityCommandBuffer(Allocator.Temp);
            base.OnUpdate();
            _ecb.Playback(EntityManager);
            _ecb.Dispose();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;

            // Don't override if we already have a valid combat target.
            if (em.HasComponent<Target>(e))
            {
                var t = em.GetComponentData<Target>(e).Value;
                if (t != Entity.Null && _ltwRO.HasComponent(t)) return TaskStatus.Success;
            }

            if (_baseQ.CalculateEntityCount() == 0) return TaskStatus.Failure;

            var baseEnt = _baseQ.GetSingletonEntity();
            var baseData = em.GetComponentData<PlayerBase>(baseEnt);
            float3 basePos = baseData.Position;

            // Push destination to base
            var dd = SystemAPI.GetComponent<DesiredDestination>(e);
            dd.Position = basePos; dd.HasValue = 1;
            SystemAPI.SetComponent(e, dd);

            // Stop at >= unit stopping distance, >= base HoldRadius
            float stopping = 0.75f;
            if (_usRO.HasComponent(e)) stopping = math.max(stopping, math.max(0f, _usRO[e].StoppingDistance));
            stopping = math.max(stopping, math.max(0f, baseData.HoldRadius));

            // Defer potential add/overwrite
            _ecb.SetOrAdd(EntityManager, e, new DesiredStoppingDistance { Value = stopping, HasValue = 1 });

            return TaskStatus.Success;
        }
    }
}
}
