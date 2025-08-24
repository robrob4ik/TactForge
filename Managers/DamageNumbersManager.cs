// Runtime/FX/DamageNumbersManager.cs
using DamageNumbersPro;
using UnityEngine;
using UnityEngine.Serialization;

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
    /// - Optional explicit camera (SetCamera). Falls back to MainCamera / any camera.
    /// - No Resources / magic paths. Profile is injected via GameConfigInstaller.
    /// - Safe if profile isn't set (logs once, ignores popups).
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class DamageNumbersManager : MonoBehaviour
    {
        private static DamageNumbersManager _instance;

        [FormerlySerializedAs("settings")]
        [FormerlySerializedAs("_profile")]
        [SerializeField] private DamageNumbersSettings profile;

        private static Camera _overrideCamera;  // optional: set from your bootstrap
        private Camera _cachedCamera;           // last good camera
        private bool _warnedNoProfile;
        private bool _warnedNoCamera;

        // Ensure a singleton object exists early (no prewarm here).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static DamageNumbersManager Ensure()
        {
            if (_instance) return _instance;

            var go = new GameObject("[DamageNumbersManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DamageNumbersManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            // NOTE: Prewarm happens when a profile is injected (SetProfile) or if one was serialized already.
            if (profile) TryPrewarm();
        }

        // Optional: let a system pick the camera explicitly (e.g., your camera bootstrap)
        public static void SetCamera(Camera cam)
        {
            var mgr = Ensure();
            _overrideCamera = cam;
            mgr._cachedCamera = cam;
        }

        // Inject the profile (called by GameConfigInstaller). Triggers prewarm once.
        public static void SetProfile(DamageNumbersSettings p)
        {
            var mgr = Ensure();
            mgr.profile = p;
            if (mgr.profile) mgr.TryPrewarm();
        }

        // Public entry point
        public static void Popup(in DamageNumbersParams p) => Ensure().PopupInternal(in p);

        // ───────────────────────────────────────────────────────── Prewarm
        private void TryPrewarm()
        {
            if (profile == null)
            {
#if UNITY_EDITOR
                if (!_warnedNoProfile)
                    Debug.LogWarning("[DamageNumbersManager] No DamageNumbersProfile assigned. " +
                                     "Assign via GameConfigInstaller or call DamageNumbersManager.SetProfile(profile).");
#endif
                _warnedNoProfile = true;
                return;
            }

            if (!profile.prewarmOnStart) return;

            void Prewarm(DamageNumber dn)
            {
                if (!dn) return;
                dn.PrewarmPool();
                for (int i = 0; i < profile.extraPrewarmCalls; i++) dn.PrewarmPool();
            }

            Prewarm(profile.damagePrefab);
            Prewarm(profile.critPrefab);
            Prewarm(profile.healPrefab);
            Prewarm(profile.dotPrefab);
            Prewarm(profile.hotPrefab);
            Prewarm(profile.blockPrefab);
            Prewarm(profile.missPrefab);
        }

        // ───────────────────────────────────────────────────────── Spawn
        private void PopupInternal(in DamageNumbersParams p)
        {
            if (profile == null)
            {
#if UNITY_EDITOR
                if (!_warnedNoProfile)
                    Debug.LogWarning("[DamageNumbersManager] No DamageNumbersProfile set — ignoring popup. " +
                                     "Assign via GameConfigInstaller or DamageNumbersManager.SetProfile(profile).");
#endif
                _warnedNoProfile = true;
                return;
            }

            float abs = Mathf.Abs(p.Amount);
            if (abs < profile.minAbsoluteValue) return;

            // Distance culling (best-effort). If no camera, don't cull (spawn anyway).
            if (profile.cullByCameraDistance)
            {
                var pos = p.Follow ? p.Follow.position : p.Position;
                var cam = GetCullCamera();
                if (cam)
                {
                    float maxSq = profile.maxSpawnDistance * profile.maxSpawnDistance;
                    if ((cam.transform.position - pos).sqrMagnitude > maxSq)
                        return; // too far
                }
            }

            var prefab = ResolvePrefab(p.Kind);
            if (!prefab)
            {
#if UNITY_EDITOR
                if (profile.logMissingPrefabWarnings)
                    Debug.LogWarning($"[DamageNumbersManager] Missing prefab for {p.Kind}.");
#endif
                return;
            }

            Vector3 spawnPos = p.Follow
                ? p.Follow.position + Vector3.up * profile.yOffset
                : p.Position + Vector3.up * profile.yOffset;

            var dn = prefab.Spawn(spawnPos, abs);

            if (profile.followTargets && p.Follow) dn.SetFollowedTarget(p.Follow);
            if (p.OverrideColor.HasValue) dn.SetColor(p.OverrideColor.Value);
        }

        private DamageNumber ResolvePrefab(DamagePopupKind kind)
        {
            return kind switch
            {
                DamagePopupKind.Damage     => profile.damagePrefab ? profile.damagePrefab : profile.critPrefab,
                DamagePopupKind.CritDamage => profile.critPrefab   ? profile.critPrefab   : profile.damagePrefab,
                DamagePopupKind.Heal       => profile.healPrefab   ? profile.healPrefab   : profile.hotPrefab,
                DamagePopupKind.Dot        => profile.dotPrefab    ? profile.dotPrefab    : profile.damagePrefab,
                DamagePopupKind.Hot        => profile.hotPrefab    ? profile.hotPrefab    : profile.healPrefab,
                DamagePopupKind.Block      => profile.blockPrefab  ? profile.blockPrefab  : profile.damagePrefab,
                DamagePopupKind.Miss       => profile.missPrefab   ? profile.missPrefab   : profile.damagePrefab,
                _                          => profile.damagePrefab
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
