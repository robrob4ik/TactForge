
using OneBitRob.Config;
using OneBitRob.ECS;
using OneBitRob.FX;

namespace OneBitRob
{
    public static class GameServices
    {
        // Scene-configured ScriptableObjects
        public static CombatLayersSettings CombatLayers { get; internal set; }
        public static DamageNumbersSettings DamageNumbers { get; internal set; }

        // Scene-level managers (Mono)
        public static ProjectilePoolManager ProjectilePools { get; internal set; }
        public static SpellVfxPoolManager  SpellVfxPools       { get; internal set; }
        public static FeedbackPoolManager FeedbackPools       { get; internal set; }
    }
}