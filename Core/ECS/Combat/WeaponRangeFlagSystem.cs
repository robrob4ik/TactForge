// File: OneBitRob/AI/WeaponRangeFlagSystem.cs

using OneBitRob.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

namespace OneBitRob.AI
{
    [UpdateInGroup(typeof(AIPlanPhaseGroup))]
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
                    // NOTE: UnitStatic is intentionally NOT required here anymore
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
                    f.Value = 0; f.DistanceSq = float.PositiveInfinity;
                    em.SetComponentData(e, f);
                    continue;
                }

                float3 selfPos = _posRO[e].Position;
                float3 tgtPos  = _posRO[targ].Position;
                float  distSq  = lengthsq(selfPos - tgtPos);

                // ---- Read melee/ranged and base range
                bool  isRanged;
                float baseRange;

                if (em.HasComponent<UnitStatic>(e))
                {
                    var us = em.GetComponentData<UnitStatic>(e);
                    isRanged  = (us.CombatStyle == 2);
                    baseRange = max(0.01f, us.AttackRangeBase);
                }
                else
                {
                    // Fallback to UnitDefinition (original behavior)
                    var brain = UnitBrainRegistry.Get(e);
                    var w     = brain?.UnitDefinition?.weapon;
                    isRanged  = (w is RangedWeaponDefinition);
                    baseRange = max(0.01f, w != null ? w.attackRange : 1.5f);
                }

                // Stats scaling remains consistent
                var stats  = em.HasComponent<UnitRuntimeStats>(e) ? em.GetComponentData<UnitRuntimeStats>(e) : UnitRuntimeStats.Defaults;
                float mult = isRanged ? stats.AttackRangeMult_Ranged : stats.AttackRangeMult_Melee;
                float range = baseRange * max(0.0001f, mult);
                float rangeSq = range * range;

                f.DistanceSq = distSq;
                f.Value      = (byte)(distSq <= rangeSq ? 1 : 0);
                em.SetComponentData(e, f);
            }
            entities.Dispose();
        }
    }
}
