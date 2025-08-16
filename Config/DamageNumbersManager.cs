using System.Collections.Generic;
using DamageNumbersPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneBitRob.FX
{
    
    public enum DamagePopupKind : byte { Damage, CritDamage, Heal, Dot, Hot, Block, Miss }
    public struct DamageNumbersParams { 
        public DamagePopupKind Kind; 
        public Vector3 Position; 
        
        public Transform Follow; 
        public float Amount; 
        public Color? OverrideColor;
    }
        
    /// <summary>
    /// Centralized entry for Damage Numbers Pro spawns with robust camera discovery:
    /// - One-time discovery with lock (optional).
    /// - Name hint → MainCamera tag → any enabled camera.
    /// - Auto re-discover if locked cameras become invalid.
    /// - Non-blocking spawns when no camera is known (distance cull is skipped).
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class DamageNumbersManager : MonoBehaviour
    {
        static DamageNumbersManager _instance;

        [SerializeField] DamageNumbersProfile _profile;

        bool _warnedNoProfile;
        bool _warnedNoCamera;

        readonly List<Camera> _runtimeCameras = new List<Camera>(4);

        // Lock behavior
        bool _lockedCameras = false;

        // ───────────────────────────────────────────────────────── Bootstrap
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => Ensure();

        public static DamageNumbersManager Ensure()
        {
            if (_instance) return _instance;

            var go = new GameObject("[DamageNumbersManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DamageNumbersManager>();

            if (_instance._profile == null)
                _instance._profile = Resources.Load<DamageNumbersProfile>("DamageNumbersProfile");

            _instance.TryPrewarm();
            _instance.RefreshCameras("bootstrap");
            return _instance;
        }

        void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            TryPrewarm();
            RefreshCameras("awake");

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _instance = null;
            }
        }

        void OnActiveSceneChanged(Scene _, Scene __) => HandleSceneEvent("activeSceneChanged");
        void OnSceneLoaded(Scene __, LoadSceneMode ___) => HandleSceneEvent("sceneLoaded");

        void HandleSceneEvent(string reason)
        {
            // If locked and we still have valid cameras, do nothing.
            // If unlocked OR cameras are invalid, refresh.
            if (_profile != null && _lockedCameras && HasValidCameras())
            {
                return;
            }

            RefreshCameras(reason);
        }

        // ───────────────────────────────────────────────────────── Public API
        public static void Popup(in DamageNumbersParams p) => Ensure().PopupInternal(in p);

        // ───────────────────────────────────────────────────────── Prewarm
        void TryPrewarm()
        {
            if (_profile == null)
            {
                if (!_warnedNoProfile)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[DamageNumbersManager] No profile assigned. Create one via Create ➜ SO/FX/Damage Numbers Profile, or place it at Resources/DamageNumbersProfile.");
#endif
                    _warnedNoProfile = true;
                }
                return;
            }

            if (_profile.prewarmOnStart)
            {
                void Prewarm(DamageNumber dn)
                {
                    if (!dn) return;
                    dn.PrewarmPool();
                    for (int i = 0; i < _profile.extraPrewarmCalls; i++) dn.PrewarmPool();
                }

                Prewarm(_profile.damagePrefab);
                Prewarm(_profile.critPrefab);
                Prewarm(_profile.healPrefab);
                Prewarm(_profile.dotPrefab);
                Prewarm(_profile.hotPrefab);
                Prewarm(_profile.blockPrefab);
                Prewarm(_profile.missPrefab);
            }
        }

        // ───────────────────────────────────────────────────────── Spawning
        void PopupInternal(in DamageNumbersParams p)
        {
            if (_profile == null) { if (!_warnedNoProfile) Debug.LogWarning("[DamageNumbersManager] No profile set."); _warnedNoProfile = true; return; }

            float abs = Mathf.Abs(p.Amount);
            if (abs < _profile.minAbsoluteValue) return;

            // Distance cull; if no camera, don't cull (spawn anyway).
            if (_profile.cullByCameraDistance)
            {
                var pos = p.Follow ? p.Follow.position : p.Position;
                if (!IsWithinAnyCameraRuntime(pos, _profile.maxSpawnDistance))
                    return;
            }

            var prefab = ResolvePrefab(p.Kind);
            if (!prefab)
            {
                if (_profile.logMissingPrefabWarnings)
                    Debug.LogWarning($"[DamageNumbersManager] Missing prefab for {p.Kind}.");
                return;
            }

            Vector3 spawnPos = p.Follow ? p.Follow.position + Vector3.up * _profile.yOffset
                                        : p.Position + Vector3.up * _profile.yOffset;

            var dn = prefab.Spawn(spawnPos, abs);
            if (_profile.followTargets && p.Follow) dn.SetFollowedTarget(p.Follow);
            if (p.OverrideColor.HasValue) dn.SetColor(p.OverrideColor.Value);
        }

        DamageNumber ResolvePrefab(DamagePopupKind kind)
        {
            return kind switch
            {
                DamagePopupKind.Damage     => _profile.damagePrefab ? _profile.damagePrefab : _profile.critPrefab,
                DamagePopupKind.CritDamage => _profile.critPrefab   ? _profile.critPrefab   : _profile.damagePrefab,
                DamagePopupKind.Heal       => _profile.healPrefab   ? _profile.healPrefab   : _profile.hotPrefab,
                DamagePopupKind.Dot        => _profile.dotPrefab    ? _profile.dotPrefab    : _profile.damagePrefab,
                DamagePopupKind.Hot        => _profile.hotPrefab    ? _profile.hotPrefab    : _profile.healPrefab,
                DamagePopupKind.Block      => _profile.blockPrefab  ? _profile.blockPrefab  : _profile.damagePrefab,
                DamagePopupKind.Miss       => _profile.missPrefab   ? _profile.missPrefab   : _profile.damagePrefab,
                _                          => _profile.damagePrefab
            };
        }

        // ───────────────────────────────────────────────────────── Camera discovery
        void RefreshCameras(string reason)
        {
            _runtimeCameras.Clear();
            
            if (_profile == null&& Camera.main)
            {
                if (!_runtimeCameras.Contains(Camera.main))
                    _runtimeCameras.Add(Camera.main);
            }
            
            PruneInvalidCameras();

            // Lock behavior
            if (_profile != null && _runtimeCameras.Count > 0)
                _lockedCameras = true;
            else
                _lockedCameras = false;

            _warnedNoCamera = false;
        }

        void EnsureCameras()
        {
            PruneInvalidCameras();

            if (_runtimeCameras.Count == 0)
            {
                // If locked but lost cams, unlock & rediscover
                if (_lockedCameras) _lockedCameras = false;
                RefreshCameras("ensure");
            }
        }

        void PruneInvalidCameras()
        {
            for (int i = _runtimeCameras.Count - 1; i >= 0; i--)
            {
                var cam = _runtimeCameras[i];
                // remove destroyed / not in a valid scene
                if (!cam || !cam.gameObject.scene.IsValid())
                    _runtimeCameras.RemoveAt(i);
            }
        }

        bool HasValidCameras()
        {
            // considers destroyed & invalid scenes
            for (int i = _runtimeCameras.Count - 1; i >= 0; i--)
            {
                var cam = _runtimeCameras[i];
                if (!cam || !cam.gameObject.scene.IsValid())
                    _runtimeCameras.RemoveAt(i);
            }
            return _runtimeCameras.Count > 0;
        }

        bool IsWithinAnyCameraRuntime(Vector3 worldPos, float maxDist)
        {
            EnsureCameras();

            if (_runtimeCameras.Count == 0)
            {
                // No camera known → don't cull (spawn anyway).
                if (!_warnedNoCamera)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("[DamageNumbersManager] No camera found for distance culling. Spawning popups anyway. " +
                                     "Tip: set 'cameraRootNameHint' in the profile to your camera root (e.g., 'Camera ---------------') or tag your camera MainCamera.");
#endif
                    _warnedNoCamera = true;
                }
                return true;
            }

            float maxSq = maxDist * maxDist;
            for (int i = 0; i < _runtimeCameras.Count; i++)
            {
                var cam = _runtimeCameras[i];
                if (!cam) continue;
                if ((cam.transform.position - worldPos).sqrMagnitude <= maxSq)
                    return true;
            }
            return false;
        }
    }
}
