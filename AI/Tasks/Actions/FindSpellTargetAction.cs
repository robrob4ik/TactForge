
using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace OneBitRob.AI
{
    [NodeDescription("Finds a valid spell target via UnitBrain strategy")]
    public class FindSpellTargetAction : AbstractTaskAction<FindSpellTargetComponent, FindSpellTargetTag, FindSpellTargetSystem>, IAction
    {
        protected override FindSpellTargetComponent CreateBufferElement(ushort runtimeIndex) { return new FindSpellTargetComponent { Index = runtimeIndex }; }
    }

    public struct FindSpellTargetComponent : IBufferElementData, ITaskCommand
    {
        public ushort Index { get; set; }
    }

    public struct FindSpellTargetTag : IComponentData, IEnableableComponent { }
    
    
    
    [DisableAutoCreation]
    public partial class FindSpellTargetSystem
        : TaskProcessorSystem<FindSpellTargetComponent, FindSpellTargetTag>
    {
       
        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            if (brain.UnitDefinition.unitSpells == null || brain.UnitDefinition.unitSpells.Count == 0)
                return TaskStatus.Failure;

            var spell    = brain.UnitDefinition.unitSpells[0];
            var strategy = SpellTargetingStrategyFactory.GetStrategy(spell.TargetingStrategyType);

            var posLookup  = GetComponentLookup<LocalTransform>(true);
            var factLookup = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);

            switch (spell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    brain.CurrentSpellTarget = strategy.GetTarget(brain, spell, ref posLookup, ref factLookup);
                    return brain.CurrentSpellTarget ? TaskStatus.Success : TaskStatus.Failure;

                case SpellTargetType.MultiTarget:
                    brain.CurrentSpellTargets = strategy.GetTargets(brain, spell, ref posLookup, ref factLookup);
                    return brain.CurrentSpellTargets != null && brain.CurrentSpellTargets.Count > 0
                        ? TaskStatus.Success
                        : TaskStatus.Failure;

                case SpellTargetType.AreaOfEffect:
                    brain.CurrentSpellTargetPosition = strategy.GetAOETargetPoint(brain, spell, ref posLookup, ref factLookup);
                    return brain.CurrentSpellTargetPosition.HasValue
                        ? TaskStatus.Success
                        : TaskStatus.Failure;
            }
            return TaskStatus.Failure;
        }
    }
}