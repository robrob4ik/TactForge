#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using OneBitRob.AI;

[InitializeOnLoad]
static class UnitBrainLabels
{
    static GUIStyle _style;

    static UnitBrainLabels()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void EnsureStyle()
    {
        if (_style != null) return;
        _style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            fontSize = 11
        };
    }

    static void OnSceneGUI(SceneView sv)
    {
        if (!Application.isPlaying) return;

        EnsureStyle();

        Handles.BeginGUI();
        try
        {
            foreach (var kv in UnitBrainRegistry.Debug_All())
            {
                var brain = kv.brain;
                if (!brain) continue;

                var hpTxt = brain.Health != null
                    ? $"{brain.Health.CurrentHealth}/{brain.Health.MaximumHealth} ({Mathf.RoundToInt((brain.Health.CurrentHealth / (float)brain.Health.MaximumHealth) * 100f)}%)"
                    : "n/a";

                var cd = Mathf.Max(0f, brain.NextAllowedAttackTime - Time.time);
                var tgt = brain.CurrentTarget ? brain.CurrentTarget.name : "—";
                float dist = brain.CurrentTarget ? Vector3.Distance(brain.transform.position, brain.CurrentTarget.transform.position) : 0f;
                float remain = brain.RemainingDistance();

                var worldPos = brain.transform.position + Vector3.up * 2f;
                var screen   = HandleUtility.WorldToGUIPoint(worldPos);

                GUI.Label(new Rect(screen.x - 80, screen.y - 38, 160, 70),
                    $"ID:{brain.GetEntity().Index}  ({(brain.UnitDefinition.isEnemy ? "EN" : "AL")})\n" + 
                    $"BT: {brain.CurrentTaskName}\n" + 
                    $"Tgt: {tgt} | d={dist:0.0} | rem={remain:0.0}", 
                    _style);
            }
        }
        finally { Handles.EndGUI(); }
    }
}
#endif
