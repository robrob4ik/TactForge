// FILE: Assets/PROJECT/Scripts/Runtime/FX/SpellVfxPoolManager.cs
//
// Changes vs prior version you have:
// - Add refCount per persistent key
// - BeginPersistentByHash increments the count and spawns/recovers once
// - EndPersistent decrements; despawns when count hits 0
// - MovePersistent(key, idHash, ...) re-acquires if instance was killed
// - PreparePersistentInstance loops ParticleSystems and disables self-stop
// - Optional Force Layer to avoid camera culling
// - One-shot Play() unchanged (short-lived)

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

        private struct PersistentEntry
        {
            public int        IdHash;
            public string     Id;
            public GameObject Go;
        }

        private static Dictionary<long, PersistentEntry> _activePersistent;
        private static Dictionary<long, int>             _refCounts;

        [Header("Debug")]
        public bool LogMissing = true;

        [Header("Visibility")]
        [Tooltip("If >= 0, forces the spawned VFX (and its children) to this layer (e.g., 0 = Default).")]
        [SerializeField] private int _forceLayer = -1;

        [Header("Persistent FX Behavior")]
        [Tooltip("Force persistent ParticleSystem(s) to loop and not auto-despawn.")]
        [SerializeField] private bool _loopPersistentParticles = true;

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
            _activePersistent = new Dictionary<long, PersistentEntry>(64);
            _refCounts        = new Dictionary<long, int>(64);
            return _instance;
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RebuildMap();
            _activePersistent ??= new Dictionary<long, PersistentEntry>(64);
            _refCounts        ??= new Dictionary<long, int>(64);
        }

        private void RebuildMap()
        {
            _map = new Dictionary<string, MMObjectPooler>(Pools.Count);
            foreach (var e in Pools)
                if (!string.IsNullOrEmpty(e.Id) && e.Pooler != null)
                    _map[e.Id] = e.Pooler;
        }

        // ─────────────────────────────── One-shots (unchanged)

        public static void PlayByHash(int idHash, Vector3 position, Transform follow = null)
        {
            if (idHash == 0) return;
            var id = OneBitRob.ECS.SpellVisualRegistry.GetVfxId(idHash);
            if (string.IsNullOrEmpty(id))
            {
#if UNITY_EDITOR
                if (_instance.LogMissing && _warned.Add($"hash:{idHash}"))
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
                    Debug.LogWarning($"[SpellFxManager] No pool mapped for id '{id}'. Add it to SpellVfxPoolManager.Pools.");
#endif
                return;
            }

            var go = pool.GetPooledGameObject();
            if (!go) return;

            _instance.PrepareOneShotInstance(go, position, follow);
        }

        private void PrepareOneShotInstance(GameObject go, Vector3 position, Transform follow)
        {
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

            if (_forceLayer >= 0) SetLayerRecursively(go, _forceLayer);

            go.SetActive(true);
            go.GetComponent<MMPoolableObject>()?.TriggerOnSpawnComplete();
        }

        // ─────────────────────────────── Persistent (ref-counted)

        public static void BeginPersistentByHash(int idHash, long key, Vector3 position, Transform follow = null)
        {
            Ensure();
            if (idHash == 0) return;

            // bump ref count
            _refCounts.TryGetValue(key, out var rc);
            _refCounts[key] = rc + 1;

            var id = OneBitRob.ECS.SpellVisualRegistry.GetVfxId(idHash);
            if (string.IsNullOrEmpty(id))
            {
#if UNITY_EDITOR
                if (_instance.LogMissing && _warned.Add($"hash:{idHash}"))
                    Debug.LogWarning($"[SpellFxManager] No VFX id registered for hash {idHash}.");
#endif
                return;
            }

            // reuse / revive if present
            if (_activePersistent.TryGetValue(key, out var entry) && entry.Go)
            {
                if (!entry.Go.activeInHierarchy)
                    _instance.PreparePersistentInstance(entry.Go, position, follow);
                else
                    _instance.MoveInstance(entry.Go, position, follow);

                // keep metadata fresh
                entry.IdHash = idHash;
                entry.Id     = id;
                _activePersistent[key] = entry;
                return;
            }

            // fresh spawn
            var go = _instance.GetFromPool(id);
            if (!go) return;

            _instance.PreparePersistentInstance(go, position, follow);
            _activePersistent[key] = new PersistentEntry { IdHash = idHash, Id = id, Go = go };
        }

        public static void MovePersistent(long key, int idHash, Vector3 position, Transform follow = null)
        {
            Ensure();

            if (!_activePersistent.TryGetValue(key, out var entry) || entry.Go == null)
            {
                // If someone moved before a begin (or if instance got destroyed), re-acquire
                var id = OneBitRob.ECS.SpellVisualRegistry.GetVfxId(idHash);
                if (string.IsNullOrEmpty(id)) return;

                var go = _instance.GetFromPool(id);
                if (!go) return;

                _instance.PreparePersistentInstance(go, position, follow);
                _activePersistent[key] = new PersistentEntry { IdHash = idHash, Id = id, Go = go };
                return;
            }

            if (!entry.Go.activeInHierarchy)
                _instance.PreparePersistentInstance(entry.Go, position, follow);
            else
                _instance.MoveInstance(entry.Go, position, follow);

            // keep idHash up to date (helps re-acquire later)
            entry.IdHash = idHash;
            _activePersistent[key] = entry;
        }

        public static void MovePersistent(long key, Vector3 position, Transform follow = null)
        {
            Ensure();
            if (_activePersistent.TryGetValue(key, out var entry))
                MovePersistent(key, entry.IdHash, position, follow);
        }

        public static void EndPersistent(long key)
        {
            Ensure();

            if (_refCounts.TryGetValue(key, out var rc))
            {
                rc -= 1;
                if (rc > 0) { _refCounts[key] = rc; return; }
                _refCounts.Remove(key);
            }

            if (_activePersistent.TryGetValue(key, out var entry))
            {
                if (entry.Go)
                {
                    var poolable = entry.Go.GetComponent<MMPoolableObject>();
                    if (poolable != null) poolable.Destroy(); else entry.Go.SetActive(false);
                }
                _activePersistent.Remove(key);
            }
        }

        // ─────────────────────────────── Helpers

        private GameObject GetFromPool(string id)
        {
            if (_map == null || !_map.TryGetValue(id, out var pool))
            {
#if UNITY_EDITOR
                if (LogMissing && _warned.Add(id))
                    Debug.LogWarning($"[SpellFxManager] No pool mapped for id '{id}'. Add it to SpellVfxPoolManager.Pools.");
#endif
                return null;
            }

            var go = pool.GetPooledGameObject();
            if (!go)
            {
#if UNITY_EDITOR
                if (LogMissing && _warned.Add($"{id}::empty"))
                    Debug.LogWarning($"[SpellFxManager] Pool for '{id}' returned null. Increase pool size or check pool setup.");
#endif
                return null;
            }
            return go;
        }

        private void PreparePersistentInstance(GameObject go, Vector3 position, Transform follow)
        {
            MoveInstance(go, position, follow);

            if (_forceLayer >= 0) SetLayerRecursively(go, _forceLayer);

            go.SetActive(true);
            go.GetComponent<MMPoolableObject>()?.TriggerOnSpawnComplete();

            if (_loopPersistentParticles)
                EnsureParticleLoop(go);
        }

        private void MoveInstance(GameObject go, Vector3 position, Transform follow)
        {
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
