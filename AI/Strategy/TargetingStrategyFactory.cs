
namespace OneBitRob.AI
{
    public static class TargetingStrategyFactory
    {
        public static readonly ITargetingStrategy ClosestEnemy = new ClosestEnemyTargeting();

        public static ITargetingStrategy GetStrategy(TargetingStrategyType type) =>
            type switch
            {
                TargetingStrategyType.ClosestEnemy => ClosestEnemy,
                _ => null
            };
    }
}