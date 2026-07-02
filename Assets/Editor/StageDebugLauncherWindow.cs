using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generic in-editor stage launcher (P4/P3 debug tooling). Lists every stage in
/// the current Play session and starts the chosen one via the same debug clock
/// reset the stone shortcut uses, so any stage — including the no-audio
/// pattern_demo — can be launched for review. The legacy "Start Stone Stage" menu
/// is kept untouched.
/// </summary>
public class StageDebugLauncherWindow : EditorWindow
{
    private const string MenuPath = "Tools/Bullet Hell/Debug/Start Stage...";
    private Vector2 scroll;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        StageDebugLauncherWindow window = GetWindow<StageDebugLauncherWindow>(true, "Start Stage");
        window.minSize = new Vector2(280, 160);
        window.Show();
    }

    private void OnGUI()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode first, then pick a stage. This launcher does not enter Play mode automatically.",
                MessageType.Info);
            return;
        }

        if (GManager.Control == null || !GManager.Control.ready || GManager.Control.SDB == null)
        {
            EditorGUILayout.HelpBox("GManager is not ready yet. Wait for the title/menu to load.", MessageType.Warning);
            if (GUILayout.Button("Refresh"))
            {
                Repaint();
            }
            return;
        }

        List<string> names = StoneStageDebugMenu.GetStageNames();
        if (names.Count == 0)
        {
            EditorGUILayout.HelpBox("No stages found in the stage database.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Pick a stage to start:", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < names.Count; i++)
        {
            string stageName = names[i];
            if (GUILayout.Button(stageName, GUILayout.Height(24)))
            {
                StoneStageDebugMenu.StartStageByName(stageName);
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
