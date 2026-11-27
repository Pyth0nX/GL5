#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps UnityEngine.UI.Selectable's static registry consistent across Editor assembly reloads,
/// Play Mode transitions, scene loads, and Prefab Stage open/close events.
/// </summary>
[InitializeOnLoad]
public static class SelectableRegistryEditorGuard
{
    static SelectableRegistryEditorGuard()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

        AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        PrefabStage.prefabStageOpened -= OnPrefabStageChanged;
        PrefabStage.prefabStageOpened += OnPrefabStageChanged;

        PrefabStage.prefabStageClosing -= OnPrefabStageChanged;
        PrefabStage.prefabStageClosing += OnPrefabStageChanged;

        EditorSceneManager.sceneOpened -= OnEditorSceneOpened;
        EditorSceneManager.sceneOpened += OnEditorSceneOpened;

        EditorSceneManager.sceneClosing -= OnEditorSceneClosing;
        EditorSceneManager.sceneClosing += OnEditorSceneClosing;

        EditorApplication.delayCall += () => SelectableRegistrySanitizer.RebuildRegistryIfNeeded(true, "EditorInitDelayCall");
    }

    private static void OnBeforeAssemblyReload()
    {
        SelectableRegistrySanitizer.RebuildRegistryIfNeeded(true, "BeforeAssemblyReload");
    }

    private static void OnAfterAssemblyReload()
    {
        SelectableRegistrySanitizer.RebuildRegistryIfNeeded(true, "AfterAssemblyReload");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.EnteredPlayMode:
            case PlayModeStateChange.ExitingPlayMode:
            case PlayModeStateChange.EnteredEditMode:
                SelectableRegistrySanitizer.RebuildRegistryIfNeeded(true, state.ToString());
                break;
        }
    }

    private static void OnPrefabStageChanged(PrefabStage stage)
    {
        SelectableRegistrySanitizer.RebuildRegistryIfNeeded(true, "PrefabStage");
    }

    private static void OnEditorSceneOpened(Scene scene, OpenSceneMode mode)
    {
        SelectableRegistrySanitizer.RebuildRegistryIfNeeded(false, "EditorSceneOpened");
    }

    private static void OnEditorSceneClosing(Scene scene, bool removingScene)
    {
        SelectableRegistrySanitizer.RebuildRegistryIfNeeded(false, "EditorSceneClosing");
    }

    [MenuItem("Tools/UI/Repair Selectable Registry")]
    private static void RepairSelectableRegistryMenu()
    {
        bool rebuilt = SelectableRegistrySanitizer.RebuildRegistryIfNeeded(true, "ManualMenu");
        Debug.Log($"[SelectableRegistryEditorGuard] Rebuild={(rebuilt ? "done" : "skipped")}, active Selectables={Selectable.allSelectableCount}.");
    }
}
#endif
