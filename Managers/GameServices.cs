// Assets/PROJECT/Scripts/Runtime/Game/GameServices.cs
using OneBitRob.AI.Debugging;
using OneBitRob.Config;
using OneBitRob.ECS;
using OneBitRob.FX;

namespace OneBitRob
{
    /// <summary>
    /// Central, read-only access to runtime services configured by MainGameManager.
    /// Keep as a thin container — no logic here.
    /// </summary>
    public static class GameServices
    {
        public static CombatLayersConfig CombatLayers { get; internal set; }
        public static DamageNumbersSettings DamageNumbers { get; internal set; }
        public static SpellDebugConfig SpellDebug { get; internal set; }
        public static ProjectilePoolManager ProjectilePools { get; internal set; }
        public static SpellVfxPoolManager SpellVfx { get; internal set; }
    }
}