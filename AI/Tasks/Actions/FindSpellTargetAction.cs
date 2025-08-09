using OneBitRob.ECS;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Entities;
using Unity.Transforms;

namespace OneBitRob.AI
{
    [NodeDescription("Finds a valid spell target via UnitBrain strategy")]
    public class FindSpellTargetAction
        : AbstractTaskAction<FindSpellTargetComponent, FindSpellTargetTag, FindSpellTargetSystem>, IAction
    {
        protected override FindSpellTargetComponent CreateBufferElement(ushort runtimeIndex)
            => new FindSpellTargetComponent { Index = runtimeIndex };
    }

    public struct FindSpellTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct FindSpellTargetTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class FindSpellTargetSystem : TaskProcessorSystem<FindSpellTargetComponent, FindSpellTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;

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
            base.OnUpdate();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain brain)
        {
            if (brain.UnitDefinition.unitSpells == null || brain.UnitDefinition.unitSpells.Count == 0)
                return TaskStatus.Failure;

            var spell    = brain.UnitDefinition.unitSpells[0];
            var strategy = SpellTargetingStrategyFactory.GetStrategy(spell.TargetingStrategyType);

            switch (spell.TargetType)
            {
                case SpellTargetType.SingleTarget:
                    brain.CurrentSpellTarget = strategy.GetTarget(brain, spell, ref _posRO, ref _factRO);
                    return brain.CurrentSpellTarget ? TaskStatus.Success : TaskStatus.Failure;

                case SpellTargetType.MultiTarget:
                    brain.CurrentSpellTargets = strategy.GetTargets(brain, spell, ref _posRO, ref _factRO);
                    return brain.CurrentSpellTargets != null && brain.CurrentSpellTargets.Count > 0
                        ? TaskStatus.Success
                        : TaskStatus.Failure;

                case SpellTargetType.AreaOfEffect:
                    brain.CurrentSpellTargetPosition = strategy.GetAOETargetPoint(brain, spell, ref _posRO, ref _factRO);
                    return brain.CurrentSpellTargetPosition.HasValue
                        ? TaskStatus.Success
                        : TaskStatus.Failure;
            }
            return TaskStatus.Failure;
        }
    }
}
