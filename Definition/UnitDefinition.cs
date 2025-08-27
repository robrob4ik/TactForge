// FILE: Assets/PROJECT/Scripts/Runtime/Config/UnitDefinition.cs
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace OneBitRob
{
    public enum TargetingStrategyType { ClosestEnemy }
    public enum CombatStrategyType { Ranged, Melee }

    public enum UnitTrait { Undead, Beast, Goblin, Human }
    public enum UnitClass { Warrior, Mage, Hunter, Assassin }
    public enum MovementType { Normal = 0, Melee = 1, Brute = 2 }

    public enum AttackAnimationSelect { Random, Sequential }

    [CreateAssetMenu(menuName = "TactForge/Definition/Unit", fileName = "UnitDefinition")]
    public class UnitDefinition : ScriptableObject
    {
        // Visual & Prefab
        [BoxGroup("Visual & Prefab")]
        [LabelText("Unit Model"), AssetsOnly]
        public GameObject unitModel;

        // Classification
        [BoxGroup("Classification")]
        [ListDrawerSettings(Expanded = true)]
        public List<UnitTrait> traits = new();

        [BoxGroup("Classification")]
        [ListDrawerSettings(Expanded = true)]
        public List<UnitClass> classes = new();

        // Shop Settings
        [BoxGroup("Shop Settings")]
        [PropertyRange(1, 5)]
        public int price = 1;

        [BoxGroup("Shop Settings")]
        [PropertyRange(1, 5)]
        public int tier = 1;

        [BoxGroup("Shop Settings")]
        [LabelText("Unit Name")]
        public string unitName;

        // Base Stats
        [BoxGroup("Base Stats")]
        [MinValue(1)]
        public int health = 100;

        [BoxGroup("Base Stats")]
        [LabelText("Move Speed"), SuffixLabel("m/s", true)]
        [MinValue(0f)]
        public float moveSpeed = 4f;

        [BoxGroup("Base Stats")]
        [LabelText("Acceleration"), SuffixLabel("m/s²", true)]
        [MinValue(0f)]
        public float acceleration = 10f;

        [BoxGroup("Base Stats")]
        public MovementType movementType = MovementType.Normal;

        // Agent
        [BoxGroup("Agent")]
        [LabelText("Stopping Distance"), SuffixLabel("units", true)]
        [MinValue(0f)]
        public float stoppingDistance = 1.5f;

        [BoxGroup("Agent")]
        [LabelText("Auto Target Min Switch Dist"), SuffixLabel("units", true)]
        [MinValue(0f)]
        public float autoTargetMinSwitchDistance = 3f;

        [BoxGroup("Agent")]
        [LabelText("Target Detection Range"), SuffixLabel("units", true)]
        [MinValue(0f)]
        public float targetDetectionRange = 10f;

        [BoxGroup("Agent")]
        [LabelText("Retarget Check Interval"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float retargetCheckInterval = 1f;

        // AI
        [BoxGroup("AI")]
        [InfoBox("While chasing, periodically return Failure from MoveToTarget to let BT re-evaluate (0 = off).")]
        [LabelText("Move Recheck Yield Interval"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float moveRecheckYieldInterval = 0.35f;

        // Team
        [BoxGroup("Team")]
        [LabelText("Is Enemy")]
        public bool isEnemy = false;

        // Strategies
        [BoxGroup("Strategies")]
        public TargetingStrategyType targetingStrategy;

        // Combat
        [BoxGroup("Combat")]
        public WeaponDefinition weapon;

        // Spells (unique per unit)
        [BoxGroup("Spells (unique per unit)")]
        [ListDrawerSettings(Expanded = false)]
        public List<SpellDefinition> unitSpells;
    }
}
