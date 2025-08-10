using OneBitRob.ECS;
using Unity.Entities;

namespace OneBitRob.AI
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    [UpdateAfter(typeof(CastSpellSystem))]
    public partial struct CastExecutionSystem : ISystem
    {
        private EntityQuery _castQuery;

        public void OnCreate(ref SystemState state)
        {
            _castQuery = state.GetEntityQuery(ComponentType.ReadWrite<CastRequest>());
            state.RequireForUpdate(_castQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            var entities = _castQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e   = entities[i];
                var req = em.GetComponentData<CastRequest>(e);
                if (req.HasValue == 0) continue;

                var brain = UnitBrainRegistry.Get(e);
                if (brain == null)
                {
                    req.HasValue = 0;
                    em.SetComponentData(e, req);
                    continue;
                }

                // Bridge ECS -> Mono targeting
                switch (req.Kind)
                {
                    case CastKind.SingleTarget:
                    {
                        var go = UnitBrainRegistry.GetGameObject(req.Target);
                        brain.CurrentSpellTarget        = go;
                        brain.CurrentSpellTargets       = null;
                        brain.CurrentSpellTargetPosition= null;
                        break;
                    }
                    case CastKind.AreaOfEffect:
                    {
                        brain.CurrentSpellTarget        = null;
                        brain.CurrentSpellTargets       = null;
                        brain.CurrentSpellTargetPosition= req.AoEPosition;
                        break;
                    }
                    default:
                        break;
                }

                // Fire if ready
                if (brain.ReadyToCastSpell() && brain.CanCastSpell())
                {
                    brain.RotateToSpellTarget();
                    var ok = brain.TryCastSpell();
                    // Whether or not we succeeded, we clear the request to avoid spamming
                }

                req.HasValue = 0;
                em.SetComponentData(e, req);
            }

            entities.Dispose();
        }
    }
}
