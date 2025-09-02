using UnityEngine;

namespace OneBitRob.Config
{
    public static class CombatLayers
    {
        private static CombatLayersSettings _settings;

        public static void Set(CombatLayersSettings settings) => _settings = settings;

        // Indices
        public static int PlayerLayerIndex => _settings.PlayerLayer;
        public static int AllyLayerIndex   => _settings.AllyLayer;
        public static int EnemyLayerIndex  => _settings.EnemyLayer;

        // Single-bit masks
        public static LayerMask PlayerMask => _settings.PlayerMask;
        public static LayerMask AllyMask   =>_settings.AllyMask;
        public static LayerMask EnemyMask  => _settings.EnemyMask;

        // Helpers
        public static int FactionLayerIndexFor(bool isEnemy) => isEnemy ? EnemyLayerIndex : AllyLayerIndex;

        public static LayerMask FriendlyMaskFor(bool isEnemy) => isEnemy ? EnemyMask : (AllyMask | PlayerMask);

        public static LayerMask HostileMaskFor(bool isEnemy) => isEnemy ? (AllyMask | PlayerMask) : EnemyMask;

        public static LayerMask TargetMaskFor(bool isEnemy) => HostileMaskFor(isEnemy);
    }
}