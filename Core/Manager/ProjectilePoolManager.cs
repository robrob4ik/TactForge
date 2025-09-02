using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.ECS
{
    [DefaultExecutionOrder(-9999)]
    public class ProjectilePoolManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string Id;               
            public MMObjectPooler Pooler;
        }

        [Tooltip("Map of projectile ids to MM poolers. Setup once per scene.")]
        public List<Entry> Pools = new();

        [Header("Debug")]
        public bool LogMissing = true;

        private static ProjectilePoolManager _instance;
        private static Dictionary<string, MMObjectPooler> _map;
        private static readonly HashSet<string> _warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static ProjectilePoolManager Ensure()
        {
            if (_instance) return _instance;
            var go = new GameObject("[ProjectilePoolManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ProjectilePoolManager>();
            _instance.RebuildMap();
            return _instance;
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RebuildMap();
        }

        private void RebuildMap()
        {
            _map = new Dictionary<string, MMObjectPooler>(Pools.Count);
            foreach (var p in Pools)
                if (p.Pooler != null && !string.IsNullOrEmpty(p.Id))
                    _map[p.Id] = p.Pooler;
        }

        /// Returns a pooler or null. Logs once if missing (if LogMissing=true).
        public static MMObjectPooler GetPooler(string id)
        {
            Ensure();
            if (string.IsNullOrEmpty(id) || _map == null || !_map.TryGetValue(id, out var pooler))
            {
#if UNITY_EDITOR
                if (_instance && _instance.LogMissing && !_warned.Contains(id ?? "<null>"))
                {
                    _warned.Add(id ?? "<null>");
                    Debug.LogWarning($"[ProjectilePoolManager] No pool for id '{id}'. Assign it on {nameof(ProjectilePoolManager)} in scene.");
                }
#endif
                return null;
            }
            return pooler;
        }

        /// Returns an inactive pooled object ready to be armed, or null if not found.
        public static GameObject GetPooled(string id)
        {
            var pooler = GetPooler(id);
            return pooler ? pooler.GetPooledGameObject() : null;
        }
    }
}
