using DamageNumbersPro;
using UnityEngine;

namespace OneBitRob.FX
{
    public enum DamagePopupKind : byte { Damage, CritDamage, Heal, Dot, Hot, Block, Miss }

    public struct DamageNumbersParams
    {
        public DamagePopupKind Kind;
        public Vector3 Position;      // used if Follow is null
        public Transform Follow;      // optional follow target
        public float Amount;          // positive magnitude; we format
        public Color? OverrideColor;  // optional color override
    }

    /// <summary>
    /// Tiny, predictable Damage Numbers bridge:
    /// - Finds a camera when needed (override -> Camera.main -> any camera).
    /// - If no camera exists, spawns anyway (skips distance cull).
    /// - No scene events, no asset-scene references.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class DamageNumbersManager : MonoBehaviour
    {
        private static DamageNumbersManager _instance;

        [SerializeField] private DamageNumbersProfile _profile;

        private static Camera _overrideCamera;  // optional: set from your bootstrap
        private Camera _cachedCamera;           // last good camera
        private bool _warnedNoProfile;
        private bool _warnedNoCamera;

        // ───────────────────────────────────────────────────────── Bootstrap
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static DamageNumbersManager Ensure()
        {
            if (_instance) return _instance;

            var go = new GameObject("[DamageNumbersManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DamageNumbersManager>();

            if (_instance._profile == null)
                _instance._profile = Resources.Load<DamageNumbersProfile>("DamageNumbersProfile");

            _instance.TryPrewarm();
            return _instance;
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            TryPrewarm();
        }

        // Optional: let a system pick the camera explicitly (e.g., your camera bootstrap)
        public static void SetCamera(Camera cam)
        {
            var mgr = Ensure();
            _overrideCamera = cam;
            mgr._cachedCamera = cam;
        }

        // Public entry point
        public static void Popup(in DamageNumbersParams p) => Ensure().PopupInternal(in p);

        // ───────────────────────────────────────────────────────── Prewarm
        private void TryPrewarm()
        {
            if (_profile == null)
            {
#if UNITY_EDITOR
                if (!_warnedNoProfile)
                    Debug.LogWarning("[DamageNumbersManager] No profile assigned. Create one via Create ➜ SO/FX/Damage Numbers Profile, or put it at Resources/DamageNumbersProfile.");
#endif
                _warnedNoProfile = true;
                return;
            }

            if (!_profile.prewarmOnStart) return;

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

        // ───────────────────────────────────────────────────────── Spawn
        private void PopupInternal(in DamageNumbersParams p)
        {
            if (_profile == null)
            {
#if UNITY_EDITOR
                if (!_warnedNoProfile)
                    Debug.LogWarning("[DamageNumbersManager] No profile set — ignoring popup.");
#endif
                _warnedNoProfile = true;
                return;
            }

            float abs = Mathf.Abs(p.Amount);
            if (abs < _profile.minAbsoluteValue) return;

            // Distance culling (best-effort). If no camera, don't cull (spawn anyway).
            if (_profile.cullByCameraDistance)
            {
                var pos = p.Follow ? p.Follow.position : p.Position;
                var cam = GetCullCamera();
                if (cam)
                {
                    float maxSq = _profile.maxSpawnDistance * _profile.maxSpawnDistance;
                    if ((cam.transform.position - pos).sqrMagnitude > maxSq)
                        return; // too far
                }
            }

            var prefab = ResolvePrefab(p.Kind);
            if (!prefab)
            {
#if UNITY_EDITOR
                if (_profile.logMissingPrefabWarnings)
                    Debug.LogWarning($"[DamageNumbersManager] Missing prefab for {p.Kind}.");
#endif
                return;
            }

            Vector3 spawnPos = p.Follow
                ? p.Follow.position + Vector3.up * _profile.yOffset
                : p.Position + Vector3.up * _profile.yOffset;

            var dn = prefab.Spawn(spawnPos, abs);

            if (_profile.followTargets && p.Follow) dn.SetFollowedTarget(p.Follow);
            if (p.OverrideColor.HasValue) dn.SetColor(p.OverrideColor.Value);
        }

        private DamageNumber ResolvePrefab(DamagePopupKind kind)
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

        // ───────────────────────────────────────────────────────── Camera discovery (simple)
        private Camera GetCullCamera()
        {
            // 1) Explicit override
            if (_overrideCamera && _overrideCamera.isActiveAndEnabled)
            {
                _cachedCamera = _overrideCamera;
                return _cachedCamera;
            }

            // 2) Cached one still valid?
            if (_cachedCamera && _cachedCamera.isActiveAndEnabled)
                return _cachedCamera;

            // 3) MainCamera
            var cam = Camera.main;
            if (!cam)
            {
                // 4) First camera in scene (includes inactive in newer Unity)
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                cam = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
#else
                cam = Object.FindObjectOfType<Camera>(true);
#endif
            }

            _cachedCamera = cam;

#if UNITY_EDITOR
            if (!cam && !_warnedNoCamera)
            {
                Debug.LogWarning("[DamageNumbersManager] No camera found. Distance culling will be skipped until a camera exists. " +
                                 "Tip: tag your gameplay camera 'MainCamera' or call DamageNumbersManager.SetCamera(cam).");
                _warnedNoCamera = true;
            }
#endif
            return cam;
        }
    }
}
