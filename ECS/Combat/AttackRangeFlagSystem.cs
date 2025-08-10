using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    /// Updates InAttackRange based on distance to Target and the unit's attackRange.
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct AttackRangeFlagSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _posRO;
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _posRO = state.GetComponentLookup<LocalTransform>(true);

            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<Target>(),
                    ComponentType.ReadWrite<InAttackRange>()
                }
            });

            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            _posRO.Update(ref state);
            var em = state.EntityManager;

            var entities = _query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];

                var f = em.GetComponentData<InAttackRange>(e);

                var targ = em.GetComponentData<Target>(e).Value;
                if (targ == Entity.Null || !_posRO.HasComponent(e) || !_posRO.HasComponent(targ))
                {
                    f.Value = 0;
                    f.DistanceSq = float.PositiveInfinity;
                    em.SetComponentData(e, f);
                    continue;
                }

                float3 selfPos = em.GetComponentData<LocalTransform>(e).Position;
                float3 tgtPos  = em.GetComponentData<LocalTransform>(targ).Position;
                float  distSq  = math.lengthsq(selfPos - tgtPos);

                // Read range from UnitDefinition via UnitBrain hybrid (cheap + simple).
                var brain = UnitBrainRegistry.Get(e);
                float range = brain != null ? math.max(0.01f, brain.UnitDefinition.attackRange) : 0.01f;
                float rangeSq = range * range;

                f.DistanceSq = distSq;
                f.Value = (byte)(distSq <= rangeSq ? 1 : 0);
                em.SetComponentData(e, f);
            }
            entities.Dispose();
        }
    }
}
