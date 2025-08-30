// File: OneBitRob/ECS/Stats/StatModifier.cs
using Unity.Entities;

namespace OneBitRob.ECS
{
    public struct StatModifier : IBufferElementData
    {
        public OneBitRob.StatKind Kind;
        public OneBitRob.StatOp   Op;
        public float              Value;
    }

    /// <summary>Mark entity to recompute its UnitRuntimeStats this frame.</summary>
    public struct StatsDirtyTag : IComponentData {}
    
    public struct UnitRuntimeStats : IComponentData
    {
        // Multipliers (defaults = 1)
        public float RangedAttackSpeedMult;
        public float MeleeAttackSpeedMult;
        public float AttackRangeMult_Ranged;
        public float AttackRangeMult_Melee;
        public float MeleeArcMult;
        public float MeleeRangeMult;
        public float SpellAoeMult;
        public float SpellRangeMult;
        public float ProjectileRadiusMult;

        // Additives (defaults = 0)
        public float CritChanceAdd;
        public float CritMultiplierMult; // multiplier, default 1 (but placed here)
        public float RangedPierceChanceAdd;
        public int   RangedPierceMaxAdd;

        public static UnitRuntimeStats Defaults => new UnitRuntimeStats
        {
            RangedAttackSpeedMult = 1f,
            MeleeAttackSpeedMult = 1f,
            AttackRangeMult_Ranged = 1f,
            AttackRangeMult_Melee = 1f,
            MeleeArcMult = 1f,
            MeleeRangeMult = 1f,
            SpellAoeMult = 1f,
            SpellRangeMult = 1f,
            ProjectileRadiusMult = 1f,

            CritChanceAdd = 0f,
            CritMultiplierMult = 1f,
            RangedPierceChanceAdd = 0f,
            RangedPierceMaxAdd = 0
        };
    }
}