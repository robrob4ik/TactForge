using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

namespace OneBitRob.VFX
{
    [DefaultExecutionOrder(-9999)]
    public sealed class VfxPoolManager : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public string Id;
            public MMObjectPooler Pooler;
        }

        [Tooltip("Map of VFX ids to MM poolers. Setup once per scene.")]
        public List<Entry> Pools = new();

        [Header("Debug")]
        public bool LogMissing = true;

        [Header("Visibility")]
        [Tooltip("Force all spawned VFX to this layer. -1 = keep prefab's layer.")]
        [SerializeField] private int _forceLayer = -1;

        [Header("Persistent FX Behavior")]
        [Tooltip("Force persistent ParticleSystem(s) to loop and not auto-despawn.")]
        [SerializeField] private bool _loopPersistentParticles = true;

        private static VfxPoolManager _instance;
        private static Dictionary<string, MMObjectPooler> _map;

        private struct PersistentEntry { public int IdHash; public string Id; public GameObject Go; }
        private static readonly Dictionary<long, PersistentEntry> _active = new(64);
        private static readonly Dictionary<long, int>             _refs   = new(64);
        private static readonly HashSet<string>                   _warned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static VfxPoolManager Ensure()
        {
            if (_instance) return _instance;
            var go = new GameObject("[VfxPoolManager]");
            DontDestroyOnLoad(go);                   // root object we just created
            _instance = go.AddComponent<VfxPoolManager>();
            _instance.RebuildMap();
            return _instance;
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            // Root-safe DDoL
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            RebuildMap();
        }

        private void RebuildMap()
        {
            _map = new Dictionary<string, MMObjectPooler>(Pools.Count);
            foreach (var e in Pools)
                if (!string.IsNullOrEmpty(e.Id) && e.Pooler != null)
                    _map[e.Id] = e.Pooler;
        }

        public static MMObjectPooler GetPooler(string id)
        {
            Ensure();
            if (string.IsNullOrEmpty(id) || _map == null || !_map.TryGetValue(id, out var pool))
            {
#if UNITY_EDITOR
                if (_instance && _instance.LogMissing && !_warned.Contains(id ?? "<null>"))
                {
                    _warned.Add(id ?? "<null>");
                    Debug.LogWarning($"[VfxPoolManager] No pool for id '{id}'. Assign it on {nameof(VfxPoolManager)} in scene.");
                }
#endif
                return null;
            }
            return pool;
        }

        public static GameObject GetPooled(string id)
        {
            var pool = GetPooler(id);
            return pool ? pool.GetPooledGameObject() : null;
        }

        public static void PlayById(string id, Vector3 position, Transform follow = null)
        {
            var pool = GetPooler(id);
            if (!pool) return;

            var go = pool.GetPooledGameObject();
            if (!go) return;

            _instance.PrepareOneShot(go, position, follow);
        }

        private void PrepareOneShot(GameObject go, Vector3 position, Transform follow)
        {
            if (follow) { go.transform.SetParent(follow, true); go.transform.position = follow.position; }
            else        { go.transform.SetParent(null, true);   go.transform.position = position;      }

            if (_forceLayer >= 0) SetLayerRecursively(go, _forceLayer);

            go.SetActive(true);
            go.GetComponent<MMPoolableObject>()?.TriggerOnSpawnComplete();
        }

        public static void BeginPersistent(string id, long key, Vector3 position, Transform follow = null)
        {
            Ensure();

            _refs.TryGetValue(key, out var rc);
            _refs[key] = rc + 1;

            var pool = GetPooler(id);
            if (!pool) return;

            if (_active.TryGetValue(key, out var entry) && entry.Go)
            {
                if (!entry.Go.activeInHierarchy)
                    _instance.PreparePersistent(entry.Go, position, follow);
                else
                    _instance.Move(entry.Go, position, follow);

                entry.IdHash = VisualAssetRegistry.RegisterVfx(id);
                entry.Id     = id;
                _active[key] = entry;
                return;
            }

            var go = pool.GetPooledGameObject();
            if (!go) return;

            _instance.PreparePersistent(go, position, follow);
            _active[key] = new PersistentEntry { IdHash = VisualAssetRegistry.RegisterVfx(id), Id = id, Go = go };
        }

        public static void MovePersistent(long key, int idHash, Vector3 position, Transform follow = null)
        {
            Ensure();

            if (!_active.TryGetValue(key, out var entry) || entry.Go == null)
            {
                var id = VisualAssetRegistry.GetVfxId(idHash);
                var pool = GetPooler(id);
                if (!pool) return;

                var go = pool.GetPooledGameObject();
                if (!go) return;

                _instance.PreparePersistent(go, position, follow);
                _active[key] = new PersistentEntry { IdHash = idHash, Id = id, Go = go };
                return;
            }

            if (!entry.Go.activeInHierarchy)
                _instance.PreparePersistent(entry.Go, position, follow);
            else
                _instance.Move(entry.Go, position, follow);

            entry.IdHash = idHash;
            _active[key] = entry;
        }

        public static void MovePersistent(long key, Vector3 position, Transform follow = null)
        {
            Ensure();
            if (_active.TryGetValue(key, out var entry))
                MovePersistent(key, entry.IdHash, position, follow);
        }

        public static void EndPersistent(long key)
        {
            Ensure();

            if (_refs.TryGetValue(key, out var rc))
            {
                rc -= 1;
                if (rc > 0) { _refs[key] = rc; return; }
                _refs.Remove(key);
            }

            if (_active.TryGetValue(key, out var entry))
            {
                if (entry.Go)
                {
                    var poolable = entry.Go.GetComponent<MMPoolableObject>();
                    if (poolable != null) poolable.Destroy(); else entry.Go.SetActive(false);
                }
                _active.Remove(key);
            }
        }

        private void PreparePersistent(GameObject go, Vector3 position, Transform follow)
        {
            Move(go, position, follow);

            if (_forceLayer >= 0) SetLayerRecursively(go, _forceLayer);

            go.SetActive(true);
            go.GetComponent<MMPoolableObject>()?.TriggerOnSpawnComplete();

            if (_loopPersistentParticles)
                EnsureParticleLoop(go);
        }

        private void Move(GameObject go, Vector3 position, Transform follow)
        {
            if (!go) return;

            if (follow) { go.transform.SetParent(follow, true); go.transform.position = follow.position; }
            else        { go.transform.SetParent(null, true);   go.transform.position = position;      }
        }

        private static void EnsureParticleLoop(GameObject root)
        {
            var ps = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                var main = p.main;
                main.loop = true;
                main.stopAction = ParticleSystemStopAction.None;
                if (!p.isPlaying) p.Play(true);
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (!go) return;
            go.layer = layer;
            foreach (Transform t in go.transform)
                SetLayerRecursively(t.gameObject, layer);
        }
    }
}
