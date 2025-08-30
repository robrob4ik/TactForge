using UnityEngine;

namespace OneBitRob.Debugging
{
    /// <summary>
    /// Tiny runtime overlay (F1 to toggle) to tweak DebugDraw without recompiling.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private DebugSettings settings;

        private static DebugOverlay _instance;
        private Rect _rect = new Rect(16, 16, 280, 180);
        private bool _visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance) return;
            var go = new GameObject("[DebugOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DebugOverlay>();
        }

        private void Awake()
        {
            if (_instance && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Try to find a settings asset if not injected by MainGameManager
            if (!settings)
            {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
                settings = FindFirstObjectByType<DebugSettings>(FindObjectsInactive.Include);
#else
                settings = Resources.Load<DebugSettings>("DebugSettings"); // optional
#endif
            }

            if (!settings) settings = ScriptableObject.CreateInstance<DebugSettings>();
            DebugDraw.SetSettings(settings);
            _visible = settings.showOverlayAtStart;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible || !settings) return;
            _rect = GUILayout.Window(GetInstanceID(), _rect, DrawWindow, "Debug Overlay");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            settings.enableDebugDraws = GUILayout.Toggle(settings.enableDebugDraws, "Enable DebugDraws");
#if !UNITY_EDITOR
            settings.allowInPlayer   = GUILayout.Toggle(settings.allowInPlayer, "Allow in Player");
#endif
            GUILayout.Space(6);
            GUILayout.Label($"Default Duration: {settings.defaultDuration:0.00}s");
            settings.defaultDuration = GUILayout.HorizontalSlider(settings.defaultDuration, 0f, 8f);

            settings.thickenLines = GUILayout.Toggle(settings.thickenLines, "Thicken Lines");
            if (settings.thickenLines)
            {
                GUILayout.Label($"Thicken Offset: {settings.thickenOffset:0.000}");
                settings.thickenOffset = GUILayout.HorizontalSlider(settings.thickenOffset, 0f, 0.05f);

                GUILayout.Label($"Thicken Passes: {settings.thickenPasses}");
                settings.thickenPasses = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.thickenPasses, 1, 4));
            }

            GUILayout.Space(6);
            GUILayout.Label("F1 - Toggle this panel");
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        // Allow MainGameManager to inject settings at boot
        public void SetSettings(DebugSettings s)
        {
            settings = s;
            DebugDraw.SetSettings(s);
            _visible = s ? s.showOverlayAtStart : false;
        }
    }
}
