
namespace OneBitRob.AI
{
    public static class SpellTargetingStrategyFactory
    {
        public static readonly ISpellTargetingStrategy Closest = new ClosestEnemySpellTargeting();
        public static readonly ISpellTargetingStrategy Cluster = new DensestEnemyClusterTargeting();
        public static readonly ISpellTargetingStrategy LowestAlly = new LowestHealthAllyTargeting();

        public static ISpellTargetingStrategy GetStrategy(SpellTargetingStrategyType type) =>
            type switch
            {
                SpellTargetingStrategyType.ClosestEnemy => Closest,
                SpellTargetingStrategyType.DensestCluster => Cluster,
                SpellTargetingStrategyType.LowestHealthAlly => LowestAlly,
                _ => null
            };
    }
}