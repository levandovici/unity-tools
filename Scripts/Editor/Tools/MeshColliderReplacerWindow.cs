using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MeshColliderReplacerWindow : EditorWindow
{
    [SerializeField]
    private List<GameObject> prefabs = new();

    [MenuItem("Tools/Mesh Collider Replacer")]
    private static void Open()
    {
        GetWindow<MeshColliderReplacerWindow>("Mesh Collider Replacer");
    }

    private SerializedObject serializedObject;
    private SerializedProperty prefabsProperty;

    private void OnEnable()
    {
        serializedObject = new SerializedObject(this);
        prefabsProperty = serializedObject.FindProperty("prefabs");
    }

    private void OnGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(prefabsProperty, true);

        GUILayout.Space(10);

        if (GUILayout.Button("Process Prefabs"))
        {
            ProcessPrefabs();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ProcessPrefabs()
    {
        int replacedRoot = 0;
        int removedChildren = 0;
        int processed = 0;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null)
                continue;

            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path))
                continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);

            bool changed = false;

            // Root MeshCollider
            MeshCollider rootMesh = root.GetComponent<MeshCollider>();
            if (rootMesh != null)
            {
                Undo.DestroyObjectImmediate(rootMesh);

                BoxCollider box = root.GetComponent<BoxCollider>();
                if (box == null)
                    box = Undo.AddComponent<BoxCollider>(root);

                FitBoxCollider(root, box);

                replacedRoot++;
                changed = true;
            }

            // Child MeshColliders
            MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);

            foreach (MeshCollider mc in colliders)
            {
                if (mc.gameObject == root)
                    continue;

                Undo.DestroyObjectImmediate(mc);
                removedChildren++;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
            processed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Processed {processed} prefabs.\n" +
            $"Root MeshColliders replaced: {replacedRoot}\n" +
            $"Child MeshColliders removed: {removedChildren}");
    }

    private static void FitBoxCollider(GameObject root, BoxCollider box)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            box.center = Vector3.zero;
            box.size = Vector3.one;
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        box.center = root.transform.InverseTransformPoint(bounds.center);

        Vector3 worldSize = bounds.size;

        Vector3 localSize = new Vector3(
            worldSize.x / root.transform.lossyScale.x,
            worldSize.y / root.transform.lossyScale.y,
            worldSize.z / root.transform.lossyScale.z);

        box.size = localSize;
    }
}