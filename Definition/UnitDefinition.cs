using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob
{
    public enum TargetingStrategyType { ClosestEnemy }
    public enum CombatStrategyType { Ranged, Melee }
    
    public enum UnitTrait { Undead, Beast, Goblin, Human }
    public enum UnitClass { Warrior, Mage, Hunter, Assassin, }
    public enum MovementType { Normal = 0, Melee = 1, Brute = 2, }
    
    public enum AttackAnimationSelect { Random, Sequential }

    [CreateAssetMenu(menuName = "SO/Units/Unit Definition")]
    public class UnitDefinition : ScriptableObject
    {
        [Header("Visual & Prefab")]
        public GameObject unitModel;

        [Header("Classification")]
        public List<UnitTrait> traits = new();
        public List<UnitClass> classes = new();

        [Header("Shop Settings")]
        [Range(1, 5)] public int price = 1;
        [Range(1, 5)] public int tier = 1;
        public string unitName;

        [Header("Base Stats")]
        public int health = 100;
        [Tooltip("m/s")] public float moveSpeed = 4f;
        public float acceleration = 10f;
        public MovementType movementType = MovementType.Normal;

        [Header("Agent")]
        [Tooltip("units")] public float stoppingDistance = 1.5f;
        [Tooltip("units")] public float autoTargetMinSwitchDistance = 3f;
        [Tooltip("units")] public float targetDetectionRange = 10f;
        [Tooltip("secs")]  public float retargetCheckInterval = 1f;

        [Header("Team")]
        public bool isEnemy = false;

        [Header("Strategies")]
        public TargetingStrategyType targetingStrategy;

        [Header("Combat")]
        public WeaponDefinition weapon;

        [Header("Spells (unique per unit)")]
        public List<SpellDefinition> unitSpells;
    }
}