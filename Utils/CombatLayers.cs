// Runtime/Config/CombatLayers.cs
using UnityEngine;

namespace OneBitRob.Config
{
    /// <summary>
    /// Centralized access to combat layers and masks.
    /// Explicitly set once at boot via GameConfigInstaller.
    /// Falls back to safe defaults if not set.
    /// </summary>
    public static class CombatLayers
    {
        private static CombatLayersConfig _config;

        /// <summary>Assign the global config at boot.</summary>
        public static void Set(CombatLayersConfig config) => _config = config;

        /// <summary>True if a config asset has been injected.</summary>
        public static bool IsConfigured => _config != null;

        // Keep for backward compatibility if any legacy code used Instance.
        // Remove after your project compiles cleanly without it.
        [System.Obsolete("CombatLayers no longer auto-loads. Inject via GameConfigInstaller and use the properties directly.")]
        public static CombatLayersConfig Instance => _config;

        // Defaults if no asset is present.
        private const int DefaultAlly  = 12;
        private const int DefaultEnemy = 11;

        public static int AllyDamageableLayer  => _config ? _config.AllyDamageableLayer  : DefaultAlly;
        public static int EnemyDamageableLayer => _config ? _config.EnemyDamageableLayer : DefaultEnemy;

        // Correct: AllyMask is the mask for Ally layer; EnemyMask is the mask for Enemy layer.
        public static LayerMask AllyMask  => _config ? _config.AllyMask  : (1 << AllyDamageableLayer);
        public static LayerMask EnemyMask => _config ? _config.EnemyMask : (1 << EnemyDamageableLayer);

        public static LayerMask TargetMaskFor(bool isEnemy) =>
            isEnemy ? AllyMask : EnemyMask;

        public static int DamageableLayerFor(bool isEnemy) =>
            isEnemy ? AllyDamageableLayer : EnemyDamageableLayer;

        public static LayerMask DamageableLayerMaskFor(bool isEnemy) =>
            1 << DamageableLayerFor(isEnemy);

        public static LayerMask FriendlyLayerMaskFor(bool isEnemy) =>
            1 << (isEnemy ? EnemyDamageableLayer : AllyDamageableLayer);
    }
}
