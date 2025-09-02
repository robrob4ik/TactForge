using System.Collections.Generic;
using OneBitRob.FX;
using Sirenix.OdinInspector;
using UnityEngine;

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
        [BoxGroup("Visual & Prefab")]
        [LabelText("Unit Model"), AssetsOnly]
        public GameObject unitModel;

        [BoxGroup("Classification")]
        [ListDrawerSettings(Expanded = true)]
        public List<UnitTrait> traits = new();

        [BoxGroup("Classification")]
        [ListDrawerSettings(Expanded = true)]
        public List<UnitClass> classes = new();

        [BoxGroup("Shop Settings")]
        [PropertyRange(1, 5)]
        public int price = 1;

        [BoxGroup("Shop Settings")]
        [PropertyRange(1, 5)]
        public int tier = 1;

        [BoxGroup("Shop Settings")]
        [LabelText("Unit Name")]
        public string unitName;

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
        public float targetDetectionRange = 100f;

        [BoxGroup("Agent")]
        [LabelText("Retarget Check Interval"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float retargetCheckInterval = 1f;

        [BoxGroup("AI")]
        [InfoBox("While chasing, periodically return Failure from MoveToTarget to let BT re-evaluate (0 = off).")]
        [LabelText("Move Recheck Yield Interval"), SuffixLabel("s", true)]
        [MinValue(0f)]
        public float moveRecheckYieldInterval = 3f;

        [BoxGroup("Team")]
        [LabelText("Is Enemy")]
        public bool isEnemy = false;

        [BoxGroup("Strategies")]
        public TargetingStrategyType targetingStrategy;

        [BoxGroup("Combat")]
        public WeaponDefinition weapon;

        [BoxGroup("Spells (unique per unit)")]
        [ListDrawerSettings(Expanded = false)]
        public List<SpellDefinition> unitSpells;

        [BoxGroup("Feedbacks")]
        [LabelText("Death Feedback")]
        [AssetsOnly] public FeedbackDefinition deathFeedback;

        [BoxGroup("Scaling")]
        [LabelText("Base Scaling (Stat Mod Set)")]
        [AssetsOnly] public StatModSetDefinition baseScaling;
    }
}
