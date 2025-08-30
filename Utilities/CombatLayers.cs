using UnityEngine;

namespace OneBitRob.Config
{
    public static class CombatLayers
    {
        private static CombatLayersSettings _settings;

        public static void Set(CombatLayersSettings settings) => _settings = settings;
        public static bool IsConfigured => _settings != null;

        // Indices
        public static int PlayerLayerIndex => _settings ? _settings.PlayerLayer : 8;
        public static int AllyLayerIndex   => _settings ? _settings.AllyLayer   : 9;
        public static int EnemyLayerIndex  => _settings ? _settings.EnemyLayer  : 10;

        // Single-bit masks
        public static LayerMask PlayerMask => _settings ? _settings.PlayerMask : (1 << PlayerLayerIndex);
        public static LayerMask AllyMask   => _settings ? _settings.AllyMask   : (1 << AllyLayerIndex);
        public static LayerMask EnemyMask  => _settings ? _settings.EnemyMask  : (1 << EnemyLayerIndex);

        // Helpers
        public static int FactionLayerIndexFor(bool isEnemy) => isEnemy ? EnemyLayerIndex : AllyLayerIndex;

        public static LayerMask FriendlyMaskFor(bool isEnemy)
            => isEnemy ? EnemyMask : (AllyMask | PlayerMask);

        public static LayerMask HostileMaskFor(bool isEnemy)
            => isEnemy ? (AllyMask | PlayerMask) : EnemyMask;

        public static LayerMask TargetMaskFor(bool isEnemy) => HostileMaskFor(isEnemy);

        public static LayerMask DamageableLayerMaskFor(bool isEnemy) => HostileMaskFor(isEnemy);

        public static LayerMask FriendlyLayerMaskFor(bool isEnemy) => FriendlyMaskFor(isEnemy);
    }
}