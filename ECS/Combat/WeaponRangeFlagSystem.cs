// File: OneBitRob.AI/WeaponRangeFlagSystem.cs

using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace OneBitRob.AI
{
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
                float baseRange = 0.01f;
                bool isRanged = false;
                if (brain != null && brain.UnitDefinition != null && brain.UnitDefinition.weapon != null)
                {
                    baseRange = math.max(0.01f, brain.UnitDefinition.weapon.attackRange);
                    isRanged  = brain.UnitDefinition.weapon is RangedWeaponDefinition;
                }

                var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;

                float mult = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;
                float range = baseRange * math.max(0.0001f, mult);
                float rangeSq = range * range;

                f.DistanceSq = distSq;
                f.Value = (byte)(distSq <= rangeSq ? 1 : 0);
                em.SetComponentData(e, f);
            }
            entities.Dispose();
        }
    }
}
