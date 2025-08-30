using OneBitRob.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace OneBitRob.ECS
{
    [BurstCompile]
    [UpdateInGroup(typeof(AITaskSystemGroup))]
    public partial struct UnitStatsRecalcSystem : ISystem
    {
        private EntityQuery _q;

        public void OnCreate(ref SystemState state)
        {
            _q = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<UnitRuntimeStats>(), ComponentType.ReadOnly<StatsDirtyTag>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var entities = _q.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];

                var stats = UnitRuntimeStats.Defaults;

                DynamicBuffer<StatModifier> mods =
                    em.HasBuffer<StatModifier>(e) ? em.GetBuffer<StatModifier>(e) : default;

                if (mods.IsCreated)
                {
                    for (int j = 0; j < mods.Length; j++)
                    {
                        var m = mods[j];
                        Apply(ref stats, in m);
                    }
                }

                // clamp sane ranges
                stats.RangedPierceChanceAdd = math.clamp(stats.RangedPierceChanceAdd, 0f, 1f);
                stats.CritMultiplierMult = math.max(0.0001f, stats.CritMultiplierMult);

                em.SetComponentData(e, stats);
                em.RemoveComponent<StatsDirtyTag>(e);
            }

            entities.Dispose();
        }

        private static void Apply(ref UnitRuntimeStats s, in StatModifier m)
        {
            // Multipliers: prefer Mul; Add is interpreted as (1 + add)
            float MulFrom(StatOp op, float value) => op == StatOp.Mul ? value : (1f + value);
            float AddFrom(StatOp op, float value) => value; // additive stays as is

            switch (m.Kind)
            {
                case StatKind.AttackSpeedMult_Ranged:
                    s.RangedAttackSpeedMult *= MulFrom(m.Op, m.Value); break;
                case StatKind.AttackSpeedMult_Melee:
                    s.MeleeAttackSpeedMult  *= MulFrom(m.Op, m.Value); break;

                case StatKind.AttackRangeMult_Ranged:
                    s.AttackRangeMult_Ranged *= MulFrom(m.Op, m.Value); break;
                case StatKind.AttackRangeMult_Melee:
                    s.AttackRangeMult_Melee  *= MulFrom(m.Op, m.Value); break;

                case StatKind.MeleeArcMult:
                    s.MeleeArcMult           *= MulFrom(m.Op, m.Value); break;
                case StatKind.MeleeRangeMult:
                    s.MeleeRangeMult         *= MulFrom(m.Op, m.Value); break;

                case StatKind.SpellAoeMult:
                    s.SpellAoeMult           *= MulFrom(m.Op, m.Value); break;
                case StatKind.SpellRangeMult:
                    s.SpellRangeMult         *= MulFrom(m.Op, m.Value); break;
                case StatKind.ProjectileRadiusMult:
                    s.ProjectileRadiusMult   *= MulFrom(m.Op, m.Value); break;

                case StatKind.CritChance_Add:
                    s.CritChanceAdd          += AddFrom(m.Op, m.Value); break;
                case StatKind.CritMultiplier_Mul:
                    s.CritMultiplierMult     *= MulFrom(m.Op, m.Value); break;

                case StatKind.RangedPierceChance_Add:
                    s.RangedPierceChanceAdd  += AddFrom(m.Op, m.Value); break;
                case StatKind.RangedPierceMax_Add:
                    s.RangedPierceMaxAdd     += (int)math.round(AddFrom(m.Op, m.Value)); break;
            }
        }
    }
}
