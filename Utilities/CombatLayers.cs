using UnityEngine;

namespace OneBitRob.Config
{
    public static class CombatLayers
    {
        private static CombatLayersSettings _settings;

        public static void Set(CombatLayersSettings settings) => _settings = settings;

        // Indices (authoritative)
        public static int PlayerLayerIndex => _settings.PlayerLayer;
        public static int AllyLayerIndex   => _settings.AllyLayer;
        public static int EnemyLayerIndex  => _settings.EnemyLayer;

        // Single-bit masks computed from indices (never trust serialized masks)
        public static LayerMask PlayerMask => (LayerMask)(1 << PlayerLayerIndex);
        public static LayerMask AllyMask   => (LayerMask)(1 << AllyLayerIndex);
        public static LayerMask EnemyMask  => (LayerMask)(1 << EnemyLayerIndex);

        // Helpers
        public static int FactionLayerIndexFor(bool isEnemy) => isEnemy ? EnemyLayerIndex : AllyLayerIndex;

        public static LayerMask FriendlyMaskFor(bool isEnemy)
        {
            // Enemy considers Enemy as friendly; Ally considers Ally + Player as friendly
            if (isEnemy)
                return EnemyMask;
            int mask = ((int)AllyMask) | ((int)PlayerMask);
            return (LayerMask)mask;
        }

        public static LayerMask HostileMaskFor(bool isEnemy)
        {
            // Enemy hits Ally+Player; Ally hits Enemy
            if (isEnemy)
            {
                int mask = ((int)AllyMask) | ((int)PlayerMask);
                return (LayerMask)mask;
            }
            return EnemyMask;
        }

        public static LayerMask TargetMaskFor(bool isEnemy) => HostileMaskFor(isEnemy);
    }
}
