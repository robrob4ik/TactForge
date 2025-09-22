using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.FX
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9999)]
    public sealed class FeedbackPoolManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string Id;
            public MMObjectPooler Pooler;
        }

        [Tooltip("Map of feedback ids to MM poolers. Setup once per scene.")]
        public List<Entry> Pools = new();

        [Header("Debug")]
        public bool LogMissing = true;

        private static FeedbackPoolManager _instance;
        private static Dictionary<string, MMObjectPooler> _map;
        private static readonly HashSet<string> _warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static FeedbackPoolManager Ensure()
        {
            if (_instance) return _instance;
            var go = new GameObject("[FeedbackPoolManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FeedbackPoolManager>();
            _instance.RebuildMap();
            return _instance;
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            RebuildMap();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Trim authoring ids to avoid accidental leading/trailing spaces
            for (int i = 0; i < Pools.Count; i++)
            {
                var e = Pools[i];
                if (!string.IsNullOrEmpty(e.Id)) e.Id = e.Id.Trim();
                Pools[i] = e;
            }
            // Editor convenience only – keeps the dictionary viewable during play mode
            if (!Application.isPlaying) RebuildMap();
        }
#endif

        private void RebuildMap()
        {
            _map = new Dictionary<string, MMObjectPooler>(Pools.Count);
#if UNITY_EDITOR
            var seen = new HashSet<string>();
#endif
            for (int i = 0; i < Pools.Count; i++)
            {
                var p = Pools[i];
                if (p.Pooler == null || string.IsNullOrEmpty(p.Id)) continue;

                var id = p.Id.Trim();
#if UNITY_EDITOR
                if (!seen.Add(id))
                {
                    Debug.LogWarning($"[FeedbackPoolManager] Duplicate id '{id}' in Pools list. Last entry wins.", this);
                }
#endif
                _map[id] = p.Pooler;
            }
        }

        public static MMObjectPooler GetPooler(string id)
        {
            Ensure();
            if (string.IsNullOrEmpty(id) || _map == null || !_map.TryGetValue(id.Trim(), out var pooler))
            {
#if UNITY_EDITOR
                if (_instance && _instance.LogMissing && !_warned.Contains(id ?? "<null>"))
                {
                    _warned.Add(id ?? "<null>");
                    Debug.LogWarning($"[FeedbackPoolManager] No pool for id '{id}'. Assign it on {nameof(FeedbackPoolManager)} in scene.");
                }
#endif
                return null;
            }
            return pooler;
        }

        public static GameObject GetPooled(string id)
        {
            // SAFE: delegate to PoolHub (auto‑repairs MM pool lists and retries)
            return PoolHub.GetPooled(PoolKind.Feedback, id);
        }
    }
}
