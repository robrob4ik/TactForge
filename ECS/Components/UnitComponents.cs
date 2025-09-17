using Unity.Entities;

namespace OneBitRob.ECS
{
    /// <summary>Frozen per-unit constants hydrated at setup from UnitDefinition and CombatLayers.</summary>
    public struct UnitStatic : IComponentData
    {
        public byte  IsEnemy;                // 0/1
        public byte  Faction;                // GameConstants.ALLY_FACTION / ENEMY_FACTION
        public byte  CombatStyle;            // 1=melee, 2=ranged

        public UnitLayerMasks Layers;        // frozen masks / damageable layer index
        public RetargetingSettings Retarget; // retarget/yield knobs

        public float AttackRangeBase;        // weapon.attackRange (authoring)
        public float StoppingDistance;       // nav stopping distance
        public float TargetDetectionRange;   // search radius for spatial hash
    }

    /// <summary>Frozen masks and the damageable physics layer index to use for hits.</summary>
    public struct UnitLayerMasks
    {
        public int FriendlyMask;             // LayerMask value
        public int HostileMask;              // LayerMask value
        public int TargetMask;               // LayerMask value
        public int DamageableLayerIndex;     // 0..31
    }

    /// <summary>Authoring intent for retarget behavior and movement yield cadence.</summary>
    public struct RetargetingSettings
    {
        public float AutoSwitchMinDistance;  // meters
        public float RetargetCheckInterval;  // seconds
        public float MoveRecheckYieldInterval; // seconds
    }
}