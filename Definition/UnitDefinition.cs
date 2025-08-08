using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob
{
    public enum CombatStrategyType
    {
        Melee,
        Ranged
    }

    public enum TargetingStrategyType
    {
        ClosestEnemy
    }

    public enum UnitTrait
    {
        Undead,
        Beast,
        Goblin,
        Human
    }

    public enum UnitClass
    {
        Warrior,
        Mage,
        Hunter,
        Assassin,
    }

    public enum MovementType
    {
        Normal = 0,
        Melee = 1,
        Brute = 2,
    }

    [CreateAssetMenu(menuName = "SO/Units/Unit Definition")]
    public class UnitDefinition : ScriptableObject
    {
        [Title("Visual & Prefab")]
        [PreviewField(100)]
        public GameObject unitModel;


        [Title("Classification")]
        [ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 10, DraggableItems = true)]
        public List<UnitTrait> traits = new();

        [ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 10, DraggableItems = true)]
        public List<UnitClass> classes = new();

        [Title("Shop Settings")]
        [Range(1, 5)]
        public int price = 1;

        [Range(1, 5)]
        public int tier = 1;

        public string unitName;

        [Title("Base Stats")]
        public int health = 100;

        [SuffixLabel("??", Overlay = true)]
        public float moveSpeed = 4f;

        public float acceleration = 10f;

        [EnumToggleButtons]
        public MovementType movementType = MovementType.Normal;

        [Title("Agent")]
        [SuffixLabel("units", Overlay = true)]
        public float stoppingDistance = 1.5f;

        [SuffixLabel("units", Overlay = true)]
        public float autoTargetMinSwitchDistance = 3f;

        [SuffixLabel("units", Overlay = true)]
        public float autoTargetDetectionRange = 10f;

        [SuffixLabel("secs", Overlay = true)]
        public float retargetCheckInterval = 1f;

        [Title("Combat")]
        [SuffixLabel("units", Overlay = true)]
        public float attackRange = 1.5f;

        [SuffixLabel("units", Overlay = true)]
        public float combatStanceDistance = 10f;

        [SuffixLabel("secs", Overlay = true)]
        public float attackCooldown = 0.5f;

        public bool isEnemy = false;

        [Title("Strategies")]
        [EnumToggleButtons]
        public TargetingStrategyType targetingStrategy;

        [EnumToggleButtons]
        public CombatStrategyType combatStrategy;

        [Title("Skills & Buffs")]
        [ListDrawerSettings(ShowPaging = true, NumberOfItemsPerPage = 10, DraggableItems = true)]
        public List<SpellDefinition> unitSpells;
    }
}