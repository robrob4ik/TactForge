// FILE: OneBitRob/AI/PlanSpellTargetAction.cs
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.GraphDesigner.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    [NodeDescription("Select spell target/point and write PlannedCast")]
    public class PlanSpellTargetAction
        : AbstractTaskAction<PlanSpellTargetComponent, PlanSpellTargetTag, PlanSpellTargetSystem>, IAction
    {
        protected override PlanSpellTargetComponent CreateBufferElement(ushort runtimeIndex)
            => new PlanSpellTargetComponent { Index = runtimeIndex };
    }

    public struct PlanSpellTargetComponent : IBufferElementData, ITaskCommand { public ushort Index { get; set; } }
    public struct PlanSpellTargetTag       : IComponentData, IEnableableComponent { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial class PlanSpellTargetSystem
        : TaskProcessorSystem<PlanSpellTargetComponent, PlanSpellTargetTag>
    {
        ComponentLookup<LocalTransform> _posRO;
        ComponentLookup<SpatialHashComponents.SpatialHashTarget> _factRO;
        ComponentLookup<HealthMirror> _hpRO;

        EntityCommandBuffer _ecb;

        protected override void OnCreate()
        {
            base.OnCreate();
            _posRO  = GetComponentLookup<LocalTransform>(true);
            _factRO = GetComponentLookup<SpatialHashComponents.SpatialHashTarget>(true);
            _hpRO   = GetComponentLookup<HealthMirror>(true);
        }

        protected override void OnUpdate()
        {
            // Refresh read-only lookups
            _posRO.Update(this);
            _factRO.Update(this);
            _hpRO.Update(this);

            // Defer all structural changes to end of system
            _ecb = new EntityCommandBuffer(Allocator.Temp);

            base.OnUpdate();

            _ecb.Playback(EntityManager);
            _ecb.Dispose();
        }

        protected override TaskStatus Execute(Entity e, UnitBrain _)
        {
            var em = EntityManager;
            if (!em.HasComponent<SpellConfig>(e)) return TaskStatus.Failure;
            if (!em.HasComponent<SpellState>(e))  return TaskStatus.Failure;

            var ss = em.GetComponentData<SpellState>(e);
            if (ss.Ready == 0) return TaskStatus.Failure;

            var cfg  = em.GetComponentData<SpellConfig>(e);
            var plan = default(PlannedCast);

            switch (cfg.Kind)
            {
                // Single-target kinds
                case SpellKind.ProjectileLine:
                case SpellKind.Chain:
                case SpellKind.EffectOverTimeTarget:
                {
                    var tgt = SelectSingleTarget(e, in cfg);
                    if (tgt != Entity.Null)
                    {
                        plan.Kind = CastKind.SingleTarget;
                        plan.Target = tgt;
                        plan.HasValue = 1;
                    }
                    break;
                }

                // AoE kind
                case SpellKind.EffectOverTimeArea:
                {
                    if (TrySelectAoE(e, in cfg, out var p))
                    {
                        plan.Kind = CastKind.AreaOfEffect;
                        plan.AoEPosition = p;
                        plan.HasValue = 1;
                    }
                    break;
                }

                // Summon at self
                case SpellKind.Summon:
                {
                    float3 pos = _posRO.HasComponent(e) ? _posRO[e].Position : float3.zero;
                    plan.Kind = CastKind.AreaOfEffect;
                    plan.AoEPosition = pos;
                    plan.HasValue = 1;
                    break;
                }
            }

            // DEFERRED write (avoids invalidating _posRO/_factRO for next entities)
            if (em.HasComponent<PlannedCast>(e)) _ecb.SetComponent(e, plan);
            else                                  _ecb.AddComponent(e, plan);

            return plan.HasValue != 0 ? TaskStatus.Success : TaskStatus.Failure;
        }

        private Entity SelectSingleTarget(Entity self, in SpellConfig cfg)
        {
            switch (cfg.AcquireMode)
            {
                case SpellAcquireMode.LowestHealthAlly:
                    return new LowestHealthAllyTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                case SpellAcquireMode.DensestEnemyCluster: // fall through to closest for single-target
                case SpellAcquireMode.ClosestEnemy:
                default:
                    return new ClosestEnemySpellTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
            }
        }

        private bool TrySelectAoE(Entity self, in SpellConfig cfg, out float3 point)
        {
            switch (cfg.AcquireMode)
            {
                case SpellAcquireMode.DensestEnemyCluster:
                    return new DensestEnemyClusterTargeting().TryGetAOETargetPoint(self, in cfg, ref _posRO, ref _factRO, out point);
                case SpellAcquireMode.LowestHealthAlly:
                case SpellAcquireMode.ClosestEnemy:
                default:
                    var tgt = new ClosestEnemySpellTargeting().GetTarget(self, in cfg, ref _posRO, ref _factRO, ref _hpRO);
                    if (tgt != Entity.Null && _posRO.HasComponent(tgt))
                    { point = _posRO[tgt].Position; return true; }
                    if (_posRO.HasComponent(self))
                    { point = _posRO[self].Position; return true; }
                    point = default; return false;
            }
        }
    }
}
