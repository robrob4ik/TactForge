#if UNITY_EDITOR
using OneBitRob.AI;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class UnitBrainLabels
{
    static readonly GUIStyle _style;

    static UnitBrainLabels()
    {
        _style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            fontSize = 11
        };
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sv)
    {
        if (Application.isPlaying == false) return;

        Handles.BeginGUI();
        try
        {
            // Iterate the registry instead of ECS query with managed components
            foreach (var kv in UnitBrainRegistry.Debug_All()) // add a Debug_All() that returns IEnumerable<(int entityIndex, UnitBrain brain)>
            {
                var brain = kv.brain;
                if (brain == null) continue;

                var worldPos = brain.transform.position + Vector3.up * 2f;
                var screen = HandleUtility.WorldToGUIPoint(worldPos);

                GUI.Label(new Rect(screen.x - 60, screen.y - 22, 120, 40),
                    $"ID:{brain.GetEntity().Index}\n{brain.CurrentTaskName}", _style);
            }
        }
        finally { Handles.EndGUI(); }
    }
}
#endif