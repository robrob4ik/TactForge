// CHANGED: removed FillObjectPool() calls to avoid duplicate child pools.

using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.ECS
{
    [DisallowMultipleComponent]
    public class ProjectilePoolManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string Id;               // e.g. "arrow", "mage_orb"
            public MMObjectPooler Pooler;   // MMSimpleObjectPooler or MMMultipleObjectPooler
        }

        [Tooltip("Map of projectile ids to MM poolers. Setup once per scene.")]
        public List<Entry> Pools = new();

        private static Dictionary<string, MMObjectPooler> _map;

        private void Awake()
        {
            if (_map == null) _map = new Dictionary<string, MMObjectPooler>(Pools.Count);
            else _map.Clear();

            foreach (var p in Pools)
            {
                if (p.Pooler == null || string.IsNullOrEmpty(p.Id)) continue;
                // DO NOT call p.Pooler.FillObjectPool() here; MM does that on its own.
                _map[p.Id] = p.Pooler;
            }
        }

        public static MMObjectPooler Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_map != null && _map.TryGetValue(id, out var pooler)) return pooler;
#if UNITY_EDITOR
            Debug.LogWarning($"ProjectilePools: no pooler mapped for id '{id}'.");
#endif
            return null;
        }
    }
}