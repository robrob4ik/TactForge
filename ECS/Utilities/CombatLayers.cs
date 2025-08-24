// FILE: Assets/PROJECT/Scripts/Runtime/Config/CombatLayers.cs
using UnityEngine;

namespace OneBitRob.Config
{
    /// <summary>
    /// Centralized access to combat layers and masks.
    /// Inject once at boot via MainGameManager.
    /// </summary>
    public static class CombatLayers
    {
        private static CombatLayersConfig _config;

        public static void Set(CombatLayersConfig config) => _config = config;
        public static bool IsConfigured => _config != null;

        // Indices
        public static int PlayerLayerIndex => _config ? _config.PlayerLayer : 8;
        public static int AllyLayerIndex   => _config ? _config.AllyLayer   : 9;
        public static int EnemyLayerIndex  => _config ? _config.EnemyLayer  : 10;

        // Single-bit masks
        public static LayerMask PlayerMask => _config ? _config.PlayerMask : (1 << PlayerLayerIndex);
        public static LayerMask AllyMask   => _config ? _config.AllyMask   : (1 << AllyLayerIndex);
        public static LayerMask EnemyMask  => _config ? _config.EnemyMask  : (1 << EnemyLayerIndex);

        /// <summary>Mask of FRIENDS for the given unit.</summary>
        public static LayerMask FriendlyMaskFor(bool isEnemy)
            => isEnemy ? EnemyMask : (AllyMask | PlayerMask);

        /// <summary>Mask of HOSTILES for the given unit.</summary>
        public static LayerMask HostileMaskFor(bool isEnemy)
            => isEnemy ? (AllyMask | PlayerMask) : EnemyMask;

        /// <summary>Layer index this unit's colliders should be on.</summary>
        public static int FactionLayerIndexFor(bool isEnemy)
            => isEnemy ? EnemyLayerIndex : AllyLayerIndex;

        // ──────────────── Backward-compat shims (keep old call sites working) ────────────────

        /// <summary>Used historically by weapons for target scans. Now returns Hostile mask.</summary>
        public static LayerMask TargetMaskFor(bool isEnemy) => HostileMaskFor(isEnemy);

        /// <summary>
        /// Historically "which layer should be hit by this unit" — that naming caused bugs.
        /// We now define it as "my own colliders' layer" to prevent self-marking as enemy.
        /// </summary>
        public static int DamageableLayerFor(bool isEnemy) => FactionLayerIndexFor(isEnemy);

        /// <summary>Historically used as hit filter. It should have been hostiles; we return hostiles now.</summary>
        public static LayerMask DamageableLayerMaskFor(bool isEnemy) => HostileMaskFor(isEnemy);

        /// <summary>Existing call sites expect a friendly mask. We map to new friendly semantics.</summary>
        public static LayerMask FriendlyLayerMaskFor(bool isEnemy) => FriendlyMaskFor(isEnemy);
    }
}
