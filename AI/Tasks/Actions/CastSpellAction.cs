// Runtime/AI/BehaviorTasks/Spell/CastSpell.cs
// Commits the cast: face the aim now; ECS executor will windup/fire.

using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using float3 = Unity.Mathematics.float3;

namespace OneBitRob.AI
{
    [NodeDescription("CastSpellAction")]
    public class CastSpellAction
        : AbstractTaskAction<CastSpellComponent, CastSpellTag, CastSpellSystem>, IAction
    {
        protected override CastSpellComponent CreateBufferElement(ushort runtimeIndex) => new CastSpellComponent { Index = runtimeIndex };
    }

    public struct CastSpellComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct CastSpellTag : IComponentData, IEnableableComponent
    {
    }

    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(SpellDecisionSystem))] // decision ready…
    [UpdateBefore(typeof(SpellWindupAndFireSystem))] // …before execution consumes it
    public partial class CastSpellSystem
        : TaskProcessorSystem<CastSpellComponent, CastSpellTag>
    {
        ComponentLookup<LocalTransform> _posRO;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO = GetComponentLookup<LocalTransform>(true);
        }

        protected override void OnUpdate()
        {
            _posRO.Update(this);
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            var em = EntityManager;

            // If we’re already mid‑cast, keep running until windup releases.
            if (em.HasComponent<SpellWindup>(e) && em.GetComponentData<SpellWindup>(e).Active != 0) return TaskStatus.Running;

            if (!em.HasComponent<CastRequest>(e) || !em.HasComponent<SpellConfig>(e)) return TaskStatus.Failure;

            var cr = em.GetComponentData<CastRequest>(e);
            if (cr.HasValue == 0) return TaskStatus.Failure;

            // Determine a facing point from the request.
            float3 aim = float3.zero;
            bool hasAim = false;

            if (cr.Kind == OneBitRob.ECS.CastKind.AreaOfEffect)
            {
                aim = cr.AoEPosition;
                hasAim = true;
            }
            else if (cr.Kind == OneBitRob.ECS.CastKind.SingleTarget && cr.Target != Entity.Null)
            {
                if (_posRO.HasComponent(cr.Target))
                {
                    aim = _posRO[cr.Target].Position;
                    hasAim = true;
                }
            }

            if (!hasAim) return TaskStatus.Failure;

            // Face the aim immediately so visual rotation starts right away.
            var df = new OneBitRob.ECS.DesiredFacing { TargetPosition = aim, HasValue = 1 };
            if (em.HasComponent<OneBitRob.ECS.DesiredFacing>(e))
                em.SetComponentData(e, df);
            else
                em.AddComponentData(e, df);

#if UNITY_EDITOR
            if (brain) Debug.DrawLine(brain.transform.position, (Vector3)aim, Color.yellow, 0f, false);
#endif
            // Do NOT consume CastRequest here — SpellExecutionSystem will pick it up this frame.
            return TaskStatus.Success;
        }
    }
}