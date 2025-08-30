using System.Collections.Generic;
using OneBitRob.ECS;
using UnityEngine;

namespace OneBitRob.VFX
{
    /// Central registry for visual assets (strings & prefabs -> stable int hashes).// Use hashes at runtime, keep human-readable strings in authoring assets.
    public static class VisualAssetRegistry
    {
        private static readonly Dictionary<int, string> _projectiles = new(64);
        private static readonly Dictionary<int, string> _vfx         = new(128);
        private static readonly Dictionary<int, GameObject> _summons  = new(32);

        private static int HashString(string id) => StableHash.String32(id);
        private static int HashPrefab(GameObject go) => go ? go.GetInstanceID() : 0;

        public static int RegisterProjectile(string id)
        {
            var h = HashString(id);
            if (h != 0 && !_projectiles.ContainsKey(h))
                _projectiles[h] = id;
            return h;
        }

        public static int RegisterVfx(string id)
        {
            var h = HashString(id);
            if (h != 0 && !_vfx.ContainsKey(h))
                _vfx[h] = id;
            return h;
        }

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

        public static void Clear()
        {
            _projectiles.Clear();
            _vfx.Clear();
            _summons.Clear();
        }
    }
}