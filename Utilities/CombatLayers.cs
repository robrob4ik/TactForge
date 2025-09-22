using UnityEngine;

namespace OneBitRob.Config
{
    public static class CombatLayers
    {
        private static CombatLayersSettings _settings;

        public static void Set(CombatLayersSettings settings) => _settings = settings;

        public static int PlayerLayerIndex => _settings.PlayerLayer;
        public static int AllyLayerIndex   => _settings.AllyLayer;
        public static int EnemyLayerIndex  => _settings.EnemyLayer;

        // Single-bit masks computed from indices (never trust serialized masks)
        public static LayerMask PlayerMask =>(1 << PlayerLayerIndex);
        public static LayerMask AllyMask   => (1 << AllyLayerIndex);
        public static LayerMask EnemyMask  => (1 << EnemyLayerIndex);

        // Helpers
        public static int FactionLayerIndexFor(bool isEnemy) => isEnemy ? EnemyLayerIndex : AllyLayerIndex;

        public static LayerMask FriendlyMaskFor(bool isEnemy)
        {
            if (isEnemy)
                return EnemyMask;
            int mask = (AllyMask) | (PlayerMask);
            return mask;
        }

        public static LayerMask HostileMaskFor(bool isEnemy)
        {
            if (isEnemy)
            {
                int mask = (AllyMask) | (PlayerMask);
                return mask;
            }
            return EnemyMask;
        }

        public static LayerMask TargetMaskFor(bool isEnemy) => HostileMaskFor(isEnemy);
    }
}
