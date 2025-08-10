// FILE: OneBitRob/AI/SpellTargetingStrategyFactory.cs
namespace OneBitRob.AI
{
    public static class SpellTargetingStrategyFactory
    {
        public static readonly ClosestEnemySpellTargeting Closest = new ClosestEnemySpellTargeting();
        public static readonly DensestEnemyClusterTargeting Cluster = new DensestEnemyClusterTargeting();
        public static readonly LowestHealthAllyTargeting LowestAlly = new LowestHealthAllyTargeting();
    }
}