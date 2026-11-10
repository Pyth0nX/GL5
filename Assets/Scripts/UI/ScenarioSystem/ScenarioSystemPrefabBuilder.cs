#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility that automatically generates and updates the standalone ScenarioSystem UI Canvas prefab
/// at Assets/Prefabs/ScenarioSystem.prefab and Assets/Prefabs/MainPrefabs/ScenarioSystem.prefab.
/// </summary>
[InitializeOnLoad]
public static class ScenarioSystemPrefabBuilder
{
    private const string PrimaryPrefabPath = "Assets/Prefabs/ScenarioSystem.prefab";
    private const string MainPrefabsPath = "Assets/Prefabs/MainPrefabs/ScenarioSystem.prefab";
    private const string ResourcesPrefabPath = "Assets/Resources/ScenarioSystem.prefab";

    static ScenarioSystemPrefabBuilder()
    {
        EditorApplication.delayCall += EnsurePrefabAssetsExist;
    }

    private static void EnsurePrefabAssetsExist()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!File.Exists(PrimaryPrefabPath) || !File.Exists(MainPrefabsPath) || !File.Exists(ResourcesPrefabPath))
        {
            CreateOrUpdateScenarioSystemPrefab();
        }
    }

    [MenuItem("Tools/ScenarioSystem/Create or Update ScenarioSystem Canvas Prefab")]
    public static void CreateOrUpdateScenarioSystemPrefab()
    {
        GameObject tempRoot = new GameObject("ScenarioSystem");
        try
        {
            ScenarioSystemUI scenarioUI = tempRoot.AddComponent<ScenarioSystemUI>();
            scenarioUI.EnsureHierarchyBuilt();
            scenarioUI.HideScenarioTree();

            EnsureDirectory("Assets/Prefabs");
            EnsureDirectory("Assets/Prefabs/MainPrefabs");
            EnsureDirectory("Assets/Resources");

            PrefabUtility.SaveAsPrefabAsset(tempRoot, PrimaryPrefabPath);
            PrefabUtility.SaveAsPrefabAsset(tempRoot, MainPrefabsPath);
            PrefabUtility.SaveAsPrefabAsset(tempRoot, ResourcesPrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ScenarioSystemPrefabBuilder] Created standalone ScenarioSystem Canvas prefab at '{PrimaryPrefabPath}', '{MainPrefabsPath}', and '{ResourcesPrefabPath}'.");
        }
        finally
        {
            Object.DestroyImmediate(tempRoot);
        }
    }

    private static void EnsureDirectory(string assetFolderPath)
    {
        if (!Directory.Exists(assetFolderPath))
        {
            Directory.CreateDirectory(assetFolderPath);
        }
    }
}
#endif
