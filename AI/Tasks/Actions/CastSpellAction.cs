using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Transforms;
using OneBitRob.ECS;
using Unity.Mathematics;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Request a spell cast based on CurrentSpell* collected earlier")]
    public class CastSpellAction : AbstractTaskAction<CastSpellComponent, CastSpellTag, CastSpellSystem>, IAction
    {
        protected override CastSpellComponent CreateBufferElement(ushort runtimeIndex) => new CastSpellComponent { Index = runtimeIndex };
    }

    public struct CastSpellComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct CastSpellTag : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class CastSpellSystem : TaskProcessorSystem<CastSpellComponent, CastSpellTag>
    {
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            if (brain.UnitDefinition.unitSpells == null || brain.UnitDefinition.unitSpells.Count == 0)
                return TaskStatus.Failure;

            var spell = brain.UnitDefinition.unitSpells[0];
            var req = default(CastRequest);

            switch (spell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    if (brain.CurrentSpellTarget == null) return TaskStatus.Failure;
                    var tgtEnt = UnitBrainRegistry.GetEntity(brain.CurrentSpellTarget);
                    if (tgtEnt == Entity.Null) return TaskStatus.Failure;
                    req.Kind = CastKind.SingleTarget;
                    req.Target = tgtEnt;
                    req.HasValue = 1;
                    break;

                case SpellTargetType.MultiTarget:
                    // Keep bridge simple: single‑target the first one for now
                    if (brain.CurrentSpellTargets == null || brain.CurrentSpellTargets.Count == 0) return TaskStatus.Failure;
                    var t0 = UnitBrainRegistry.GetEntity(brain.CurrentSpellTargets[0]);
                    if (t0 == Entity.Null) return TaskStatus.Failure;
                    req.Kind = CastKind.SingleTarget;
                    req.Target = t0;
                    req.HasValue = 1;
                    break;

                case SpellTargetType.AreaOfEffect:
                    if (!brain.CurrentSpellTargetPosition.HasValue) return TaskStatus.Failure;
                    req.Kind = CastKind.AreaOfEffect;
                    req.AoEPosition = brain.CurrentSpellTargetPosition.Value;
                    req.HasValue = 1;
                    break;
            }

            EntityManager.SetComponentData(e, req);
            return TaskStatus.Success;
        }
    }
}
