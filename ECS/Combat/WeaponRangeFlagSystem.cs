// Assets/PROJECT/Scripts/Runtime/AI/Combat/Weapon/WeaponRangeFlagSystem.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using OneBitRob.AI;
using OneBitRob.ECS;

namespace OneBitRob.AI
{
    /// <summary>
    /// Updates InAttackRange based on distance(self, target) vs weapon.attackRange.
    /// Pure ECS; UnitBrain only used to read weapon stat.
    /// </summary>
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct WeaponRangeFlagSystem : ISystem
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

                float3 selfPos = _posRO[e].Position;
                float3 tgtPos  = _posRO[targ].Position;
                float  distSq  = math.lengthsq(selfPos - tgtPos);

                var brain = UnitBrainRegistry.Get(e);
                float range = 0.01f;
                if (brain != null && brain.UnitDefinition != null && brain.UnitDefinition.weapon != null)
                    range = math.max(0.01f, brain.UnitDefinition.weapon.attackRange);

                float rangeSq = range * range;

                f.DistanceSq = distSq;
                f.Value = (byte)(distSq <= rangeSq ? 1 : 0);
                em.SetComponentData(e, f);
            }
            entities.Dispose();
        }
    }
}
