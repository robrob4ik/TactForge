using Unity.Entities;

namespace OneBitRob.ECS
{
    // TODO A lot of unused stuff, either remove or use
    public struct UnitStatic : IComponentData
    {
        public byte IsEnemy; // 0/1
        public byte Faction; // GameConstants.ALLY_FACTION / ENEMY_FACTION
        public byte CombatStyle; // 1=melee, 2=ranged

        public UnitLayerMasks Layers; 
        public RetargetingSettings Retarget; 

        public float AttackRangeBase; 
        public float StoppingDistance; 
        public float TargetDetectionRange;
    }

    public struct UnitLayerMasks
    {
        public int FriendlyMask; // LayerMask value
        public int HostileMask; // LayerMask value
        public int TargetMask; // LayerMask value
        public int DamageableLayerIndex; // 0..31
    }

    public struct RetargetingSettings
    {
        public float AutoSwitchMinDistance; 
        public float RetargetCheckInterval;
        public float MoveRecheckYieldInterval; 
    }
}