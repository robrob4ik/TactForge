using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct StatModifier : IBufferElementData
    {
        public StatKind Kind;
        public StatOp   Op;
        public float    Value;
    }
    public struct StatsDirtyTag : IComponentData {}
    public struct UnitRuntimeStats : IComponentData
    {
        public float RangedAttackSpeedMult;
        public float MeleeAttackSpeedMult;
        public float AttackRangeMult_Ranged;
        public float AttackRangeMult_Melee;
        public float MeleeArcMult;
        public float MeleeRangeMult;
        public float SpellAoeMult;
        public float SpellRangeMult;
        public float ProjectileRadiusMult;

        public float CritChanceAdd;
        public float CritMultiplierMult;
        public float RangedPierceChanceAdd;
        public int   RangedPierceMaxAdd;

        public static UnitRuntimeStats Defaults => new UnitRuntimeStats
        {
            RangedAttackSpeedMult   = 1f,
            MeleeAttackSpeedMult    = 1f,
            AttackRangeMult_Ranged  = 1f,
            AttackRangeMult_Melee   = 1f,
            MeleeArcMult            = 1f,
            MeleeRangeMult          = 1f,
            SpellAoeMult            = 1f,
            SpellRangeMult          = 1f,
            ProjectileRadiusMult    = 1f,

            CritChanceAdd           = 0f,
            CritMultiplierMult      = 1f,
            RangedPierceChanceAdd   = 0f,
            RangedPierceMaxAdd      = 0
        };
    }
}