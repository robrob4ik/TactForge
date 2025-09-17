// File: OneBitRob/Editor/UnitRegistryWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Entities;
using OneBitRob.AI;

namespace OneBitRob.EditorTools
{
    public class UnitRegistryWindow : EditorWindow
    {
        [MenuItem("Tools/TactForge/Unit Registry")]
        public static void Open() => GetWindow<UnitRegistryWindow>("Unit Registry");

        private Vector2 _scroll;

        private void OnGUI()
        {
            GUILayout.Label("UnitBrainRegistry — Live Mappings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var (index, version, brain) in UnitBrainRegistry.Debug_All())
            {
                if (!brain) continue;

                EditorGUILayout.BeginVertical("box");
                GUILayout.Label(brain.name, EditorStyles.boldLabel);
                GUILayout.Label($"Entity: Index={index} Version={version}");
                GUILayout.Label($"Faction: {(brain.UnitDefinition != null && brain.UnitDefinition.isEnemy ? "Enemy" : "Ally")}");
                if (GUILayout.Button("Select GameObject")) Selection.activeObject = brain.gameObject;
                if (GUILayout.Button("Ping")) EditorGUIUtility.PingObject(brain.gameObject);

                // Animator sanity (triggers exist?)
                var anim = brain.GetComponentInChildren<Animator>();
                if (!anim) GUILayout.Label("Animator: <none>");
                else GUILayout.Label($"Animator: OK ({anim.parameterCount} params)");

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Refresh")) Repaint();
        }
    }
}
#endif