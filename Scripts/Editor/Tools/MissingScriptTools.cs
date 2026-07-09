using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MissingScriptTools : EditorWindow
{
    private enum Tab
    {
        Scene,
        Prefab
    }

    private class Entry
    {
        public GameObject gameObject;
        public int missingCount;
        public string path;
    }

    private Tab currentTab;
    private Vector2 scroll;
    private readonly List<Entry> results = new();

    [MenuItem("Tools/Missing Script Tools")]
    static void Open()
    {
        GetWindow<MissingScriptTools>("Missing Script Tools");
    }

    private void OnGUI()
    {
        GUILayout.Space(5);

        currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new[]
        {
            "Scene",
            "Prefab"
        });

        GUILayout.Space(10);

        switch (currentTab)
        {
            case Tab.Scene:
                DrawSceneTab();
                break;

            case Tab.Prefab:
                DrawPrefabTab();
                break;
        }

        GUILayout.Space(10);

        EditorGUILayout.LabelField($"Objects Found: {results.Count}", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (Entry entry in results)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(entry.path, GUILayout.Height(20)))
            {
                Selection.activeGameObject = entry.gameObject;
                EditorGUIUtility.PingObject(entry.gameObject);
            }

            GUILayout.Label($"Missing: {entry.missingCount}", GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    #region Scene

    void DrawSceneTab()
    {
        EditorGUILayout.HelpBox("Searches only the currently open Scene.", MessageType.Info);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Find Missing Scripts", GUILayout.Height(35)))
        {
            FindScene();
        }

        if (GUILayout.Button("Remove Missing Scripts", GUILayout.Height(35)))
        {
            RemoveAll();
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Save Scene"))
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Scene saved.");
        }
    }

    void FindScene()
    {
        results.Clear();

        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Scan(root);
        }

        Debug.Log($"Found {results.Count} GameObjects with missing scripts.");
    }

    #endregion

    #region Prefab

    void DrawPrefabTab()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

        if (stage == null)
        {
            EditorGUILayout.HelpBox(
                "No Prefab is currently opened.\n\nOpen a Prefab in Prefab Mode.",
                MessageType.Warning);

            return;
        }

        EditorGUILayout.LabelField("Current Prefab", stage.assetPath);

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Find Missing Scripts", GUILayout.Height(35)))
        {
            FindPrefab(stage);
        }

        if (GUILayout.Button("Remove Missing Scripts", GUILayout.Height(35)))
        {
            RemoveAll();
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Save Prefab"))
        {
            PrefabUtility.SavePrefabAsset(stage.prefabContentsRoot);
            AssetDatabase.SaveAssets();

            Debug.Log("Prefab saved.");
        }
    }

    void FindPrefab(PrefabStage stage)
    {
        results.Clear();

        Scan(stage.prefabContentsRoot);

        Debug.Log($"Found {results.Count} GameObjects with missing scripts.");
    }

    #endregion

    #region Scan

    void Scan(GameObject go)
    {
        Component[] comps = go.GetComponents<Component>();

        int missing = 0;

        foreach (Component c in comps)
        {
            if (c == null)
                missing++;
        }

        if (missing > 0)
        {
            results.Add(new Entry
            {
                gameObject = go,
                missingCount = missing,
                path = GetPath(go)
            });
        }

        foreach (Transform child in go.transform)
        {
            Scan(child.gameObject);
        }
    }

    #endregion

    #region Remove

    void RemoveAll()
    {
        if (results.Count == 0)
        {
            Debug.Log("Nothing to remove.");
            return;
        }

        int objects = 0;
        int removed = 0;

        foreach (Entry entry in results)
        {
            if (entry.gameObject == null)
                continue;

            Undo.RegisterCompleteObjectUndo(entry.gameObject, "Remove Missing Scripts");

            int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(entry.gameObject);

            if (count > 0)
            {
                removed += count;
                objects++;

                EditorUtility.SetDirty(entry.gameObject);

                if (entry.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(entry.gameObject.scene);
            }
        }

        Debug.Log($"Removed {removed} missing scripts from {objects} GameObjects.");

        results.Clear();
    }

    #endregion

    static string GetPath(GameObject go)
    {
        string path = go.name;

        Transform t = go.transform.parent;

        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }

        return path;
    }
}