namespace OneBitRob.AI {
    public static class CombatStrategyFactory
    {
        private static readonly ICombatStrategy melee = new MeleeCombatStrategy();
        private static readonly ICombatStrategy ranged = new RangedCombatStrategy();

        public static ICombatStrategy GetStrategy(CombatStrategyType type) =>
            type switch
            {
                CombatStrategyType.Melee => melee,
                CombatStrategyType.Ranged => ranged,
                _ => null
            };
    }

}