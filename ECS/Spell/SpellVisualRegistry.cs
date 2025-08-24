// Runtime/ECS/Spell/SpellVisualRegistry.cs
using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// <summary>
    /// Maps authoring IDs (strings/prefabs) to stable int hashes for ECS.
    /// IMPORTANT:
    /// - Strings are hashed with StableHash.String32 (deterministic across runs).
    /// - Prefabs are mapped by InstanceID at runtime (sufficient for spell summons).
    /// </summary>
    public static class SpellVisualRegistry
    {
        private static readonly Dictionary<int, string> _projectiles = new(64);
        private static readonly Dictionary<int, string> _vfx         = new(128);
        private static readonly Dictionary<int, GameObject> _summons  = new(32);

        private static int HashString(string id) => StableHash.String32(id);
        private static int HashPrefab(GameObject go) => go ? go.GetInstanceID() : 0;

        /// <summary>Register a projectile pool ID (e.g., "arrow", "mage_orb") and return its stable hash.</summary>
        public static int RegisterProjectile(string id)
        {
            var h = HashString(id);
            if (h != 0 && !_projectiles.ContainsKey(h))
                _projectiles[h] = id;
            return h;
        }

        /// <summary>Register a VFX id (e.g., "heal_aura") and return its stable hash.</summary>
        public static int RegisterVfx(string id)
        {
            var h = HashString(id);
            if (h != 0 && !_vfx.ContainsKey(h))
                _vfx[h] = id;
            return h;
        }

        /// <summary>Register a summon prefab and return its (runtime) stable key for this session.</summary>
        public static int RegisterSummon(GameObject prefab)
        {
            var h = HashPrefab(prefab);
            if (h != 0 && !_summons.ContainsKey(h))
                _summons[h] = prefab;
            return h;
        }

        public static string GetProjectileId(int hash)
            => (hash != 0 && _projectiles.TryGetValue(hash, out var id)) ? id : null;

        public static string GetVfxId(int hash)
            => (hash != 0 && _vfx.TryGetValue(hash, out var id)) ? id : null;

        public static GameObject GetSummonPrefab(int hash)
            => (hash != 0 && _summons.TryGetValue(hash, out var go)) ? go : null;

        /// <summary>Clear all maps (mainly for domain reloads in editor).</summary>
        public static void Clear()
        {
            _projectiles.Clear();
            _vfx.Clear();
            _summons.Clear();
        }
    }
}
