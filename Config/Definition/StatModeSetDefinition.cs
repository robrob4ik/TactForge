// File: OneBitRob/Definitions/StatModSetDefinition.cs
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob
{
    public enum StatKind : byte
    {
        // Multipliers
        AttackSpeedMult_Ranged,
        AttackSpeedMult_Melee,
        AttackRangeMult_Ranged,
        AttackRangeMult_Melee,
        MeleeArcMult,
        MeleeRangeMult,
        SpellAoeMult,
        SpellRangeMult,
        ProjectileRadiusMult,

        // Additives
        CritChance_Add,
        CritMultiplier_Mul,     // treated as multiplier
        RangedPierceChance_Add,
        RangedPierceMax_Add
    }

    public enum StatOp : byte { Add, Mul }

    [System.Serializable]
    public struct StatModifierData
    {
        public StatKind Kind;
        public StatOp   Op;
        public float    Value;
    }

    [CreateAssetMenu(menuName = "TactForge/Config/Stats/Stat Mod Set", fileName = "StatModSet")]
    public sealed class StatModSetDefinition : ScriptableObject
    {
        [Tooltip("Ordered list of stat modifiers (applied top-to-bottom).")]
        public List<StatModifierData> entries = new();
    }
}