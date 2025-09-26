using Unity.Entities;

namespace OneBitRob.ECS
{
   
    public struct UnitStatic : IComponentData
    {
        public byte IsEnemy; 
        public byte Faction; 
        public byte CombatStyle;

        public RetargetingSettings Retarget; 

        public float AttackRangeBase; 
        public float StoppingDistance; 
        public float TargetDetectionRange;
    }
    

    public struct RetargetingSettings
    {
        
        public float AutoSwitchMinDistance; 
        public float RetargetCheckInterval;
        public float MoveRecheckYieldInterval; 
    }
}