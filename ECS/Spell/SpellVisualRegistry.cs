using System.Collections.Generic;
using UnityEngine;

namespace OneBitRob.ECS
{
    /// Maps ScriptableObject string keys & prefabs to stable int hashes for ECS.
    public static class SpellVisualRegistry
    {
        static readonly Dictionary<int, string> _projectiles = new();
        static readonly Dictionary<int, string> _vfx = new();
        static readonly Dictionary<int, GameObject> _summons = new();

        static int Hash(string s) => string.IsNullOrEmpty(s) ? 0 : s.GetHashCode(); // stable enough per process
        static int Hash(GameObject go) => go ? go.GetInstanceID() : 0;

        public static int RegisterProjectile(string id)
        {
            int h = Hash(id);
            if (h != 0 && !_projectiles.ContainsKey(h)) _projectiles[h] = id;
            return h;
        }

        public static int RegisterVfx(string id)
        {
            int h = Hash(id);
            if (h != 0 && !_vfx.ContainsKey(h)) _vfx[h] = id;
            return h;
        }

        public static int RegisterSummon(GameObject prefab)
        {
            int h = Hash(prefab);
            if (h != 0 && !_summons.ContainsKey(h)) _summons[h] = prefab;
            return h;
        }

        public static string GetProjectileId(int hash) => (hash != 0 && _projectiles.TryGetValue(hash, out var id)) ? id : null;
        public static string GetVfxId(int hash) => (hash != 0 && _vfx.TryGetValue(hash, out var id)) ? id : null;
        public static GameObject GetSummonPrefab(int hash) => (hash != 0 && _summons.TryGetValue(hash, out var go)) ? go : null;
    }
}