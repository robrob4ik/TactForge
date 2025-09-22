using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

[UpdateInGroup(typeof(AIPlanPhaseGroup))]
public partial struct WeaponRangeFlagSystem : ISystem
{
    private ComponentLookup<LocalTransform> _posRO;
    private EntityQuery _query;

    public void OnCreate(ref SystemState state)
    {
        _posRO = state.GetComponentLookup<LocalTransform>(true);
        _query = state.GetEntityQuery(
            new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<Target>(),
                    ComponentType.ReadWrite<InAttackRange>(),
                    ComponentType.ReadOnly<UnitStatic>() // ⬅️ mandatory
                }
            }
        );
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
            var flag = em.GetComponentData<InAttackRange>(e);

            var targetEnt = em.GetComponentData<Target>(e).Value;
            if (targetEnt == Entity.Null || !_posRO.HasComponent(e) || !_posRO.HasComponent(targetEnt))
            {
                flag.Value = 0;
                flag.DistanceSq = float.PositiveInfinity;
                em.SetComponentData(e, flag);
                continue;
            }

            float3 selfPos = _posRO[e].Position;
            float3 tgtPos  = _posRO[targetEnt].Position;
            float  distSq  = lengthsq(selfPos - tgtPos);

            var us = em.GetComponentData<UnitStatic>(e);
            bool  isRanged   = (us.CombatStyle == 2);
            float baseRange  = max(0.01f, us.AttackRangeBase);

            var stats = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;
            float mult       = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;
            float range      = baseRange * max(0.0001f, mult);
            float rangeSq    = range * range;

            flag.DistanceSq = distSq;
            flag.Value      = (byte)(distSq <= rangeSq ? 1 : 0);
            em.SetComponentData(e, flag);
        }

        entities.Dispose();
    }
}
