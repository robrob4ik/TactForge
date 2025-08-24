using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.FX
{
    [DefaultExecutionOrder(-9999)]
    public sealed class SpellVfxPoolManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string Id;            // e.g., "heal_aura", "aoe_fire"
            public MMObjectPooler Pooler;
        }

        public List<Entry> Pools = new();

        private static SpellVfxPoolManager _instance;
        private static Dictionary<string, MMObjectPooler> _map;

        // NEW: persistent instances keyed by (entity index/version + vfx id hash)
        private static Dictionary<long, GameObject> _activePersistent;

        public bool LogMissing = true;
        private static readonly HashSet<string> _warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static SpellVfxPoolManager Ensure()
        {
            if (_instance) return _instance;
            var go = new GameObject("[SpellFxManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SpellVfxPoolManager>();
            _instance.RebuildMap();
            _activePersistent = new Dictionary<long, GameObject>(32);
            return _instance;
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RebuildMap();
            if (_activePersistent == null) _activePersistent = new Dictionary<long, GameObject>(32);
        }

        private void RebuildMap()
        {
            _map = new Dictionary<string, MMObjectPooler>(Pools.Count);
            foreach (var e in Pools)
                if (!string.IsNullOrEmpty(e.Id) && e.Pooler != null)
                    _map[e.Id] = e.Pooler;
        }

        // ────────────────────────────────────────── Legacy one-shot helper (still available)
        public static void PlayByHash(int idHash, Vector3 position, Transform follow = null)
        {
            if (idHash == 0) return;
            var id = OneBitRob.ECS.SpellVisualRegistry.GetVfxId(idHash);
            if (string.IsNullOrEmpty(id))
            {
#if UNITY_EDITOR
                if (_instance.LogMissing && _warned.Add(idHash.ToString()))
                    Debug.LogWarning($"[SpellFxManager] No VFX id registered for hash {idHash}.");
#endif
                return;
            }
            Play(id, position, follow);
        }

        public static void Play(string id, Vector3 position, Transform follow = null)
        {
            Ensure();
            if (_map == null || !_map.TryGetValue(id, out var pool))
            {
#if UNITY_EDITOR
                if (_instance.LogMissing && _warned.Add(id))
                    Debug.LogWarning($"[SpellFxManager] No pool mapped for id '{id}'. Add it to SpellFxManager.Pools.");
#endif
                return;
            }

            var go = pool.GetPooledGameObject();
            if (!go) return;

            if (follow)
            {
                go.transform.SetParent(follow, worldPositionStays: true);
                go.transform.position = follow.position;
            }
            else
            {
                go.transform.SetParent(null, true);
                go.transform.position = position;
            }

            go.SetActive(true);
            var poolable = go.GetComponent<MMPoolableObject>();
            poolable?.TriggerOnSpawnComplete();
        }

        // ────────────────────────────────────────── NEW persistent helpers
        public static void BeginPersistentByHash(int idHash, long key, Vector3 position, Transform follow = null)
        {
            Ensure();
            if (idHash == 0) return;
            if (_activePersistent.TryGetValue(key, out var existing) && existing)
            {
                MovePersistent(key, position, follow);
                return;
            }

            var id = OneBitRob.ECS.SpellVisualRegistry.GetVfxId(idHash);
            if (string.IsNullOrEmpty(id))
            {
#if UNITY_EDITOR
                if (_instance.LogMissing && _warned.Add($"hash:{idHash}"))
                    Debug.LogWarning($"[SpellFxManager] No VFX id registered for hash {idHash}.");
#endif
                return;
            }

            if (_map == null || !_map.TryGetValue(id, out var pool))
            {
#if UNITY_EDITOR
                if (_instance.LogMissing && _warned.Add(id))
                    Debug.LogWarning($"[SpellFxManager] No pool mapped for id '{id}'. Add it to SpellFxManager.Pools.");
#endif
                return;
            }

            var go = pool.GetPooledGameObject();
            if (!go) return;

            if (follow)
            {
                go.transform.SetParent(follow, true);
                go.transform.position = follow.position;
            }
            else
            {
                go.transform.SetParent(null, true);
                go.transform.position = position;
            }

            go.SetActive(true);
            go.GetComponent<MMPoolableObject>()?.TriggerOnSpawnComplete();
            _activePersistent[key] = go;
        }

        public static void MovePersistent(long key, Vector3 position, Transform follow = null)
        {
            if (_activePersistent == null) return;
            if (!_activePersistent.TryGetValue(key, out var go) || !go) return;

            if (follow)
            {
                go.transform.SetParent(follow, true);
                go.transform.position = follow.position;
            }
            else
            {
                go.transform.SetParent(null, true);
                go.transform.position = position;
            }
        }

        public static void EndPersistent(long key)
        {
            if (_activePersistent == null) return;
            if (_activePersistent.TryGetValue(key, out var go) && go)
            {
                var poolable = go.GetComponent<MMPoolableObject>();
                if (poolable != null) poolable.Destroy(); else go.SetActive(false);
            }
            _activePersistent.Remove(key);
        }

        public static bool HasPersistent(long key)
            => _activePersistent != null && _activePersistent.ContainsKey(key);
    }
}
