using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Guards and repairs UnityEngine.UI.Selectable's internal static registry (s_Selectables / s_SelectableCount)
/// when assembly reloads, prefab stage cleanups, or Play Mode transitions leave m_EnableCalled / count out of sync.
/// </summary>
public static class SelectableRegistrySanitizer
{
    private static readonly FieldInfo s_SelectablesField;
    private static readonly FieldInfo s_SelectableCountField;
    private static readonly FieldInfo s_CurrentIndexField;
    private static readonly FieldInfo s_EnableCalledField;
    private static readonly bool s_reflectionReady;
    private static bool s_sceneLoadedSubscribed;

    static SelectableRegistrySanitizer()
    {
        try
        {
            Type selectableType = typeof(Selectable);
            s_SelectablesField = selectableType.GetField("s_Selectables", BindingFlags.Static | BindingFlags.NonPublic);
            s_SelectableCountField = selectableType.GetField("s_SelectableCount", BindingFlags.Static | BindingFlags.NonPublic);
            s_CurrentIndexField = selectableType.GetField("m_CurrentIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            s_EnableCalledField = selectableType.GetField("m_EnableCalled", BindingFlags.Instance | BindingFlags.NonPublic);

            s_reflectionReady =
                s_SelectablesField != null &&
                s_SelectableCountField != null &&
                s_CurrentIndexField != null &&
                s_EnableCalledField != null;
        }
        catch (Exception)
        {
            s_reflectionReady = false;
        }
    }

    /// <summary>
    /// Checks whether Selectable's static registry array or per-instance indices are out of sync.
    /// </summary>
    public static bool IsRegistryCorrupted()
    {
        if (!s_reflectionReady)
        {
            return false;
        }

        try
        {
            Selectable[] arr = s_SelectablesField.GetValue(null) as Selectable[];
            int count = (int)s_SelectableCountField.GetValue(null);
            if (arr == null || count < 0 || count > arr.Length)
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                Selectable sel = arr[i];
                if (sel == null || !sel.isActiveAndEnabled)
                {
                    return true;
                }

                int currentIndex = (int)s_CurrentIndexField.GetValue(sel);
                bool enableCalled = (bool)s_EnableCalledField.GetValue(sel);
                if (currentIndex != i || !enableCalled)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Rebuilds Selectable.s_Selectables and synchronizes m_CurrentIndex / m_EnableCalled for all live Selectables.
    /// Returns true if a rebuild was performed.
    /// </summary>
    public static bool RebuildRegistryIfNeeded(bool forceRebuild = false, string reason = null)
    {
        if (!s_reflectionReady)
        {
            return false;
        }

        if (!forceRebuild && !IsRegistryCorrupted())
        {
            return false;
        }

        try
        {
            Selectable[] all = Resources.FindObjectsOfTypeAll<Selectable>();
            HashSet<Selectable> seen = new HashSet<Selectable>();
            List<Selectable> activeList = new List<Selectable>(all != null ? all.Length : 16);

            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    Selectable sel = all[i];
                    if (sel == null || sel.gameObject == null || !sel.gameObject.scene.IsValid())
                    {
                        continue;
                    }

#if UNITY_EDITOR
                    if (UnityEditor.EditorUtility.IsPersistent(sel))
                    {
                        continue;
                    }
#endif

                    if (!seen.Add(sel))
                    {
                        continue;
                    }

                    if (sel.isActiveAndEnabled)
                    {
                        activeList.Add(sel);
                    }
                    else
                    {
                        s_CurrentIndexField.SetValue(sel, -1);
                        s_EnableCalledField.SetValue(sel, false);
                    }
                }
            }

            int capacity = NextPowerOfTwo(Mathf.Max(16, activeList.Count));
            Selectable[] rebuilt = new Selectable[capacity];
            for (int i = 0; i < activeList.Count; i++)
            {
                Selectable activeSel = activeList[i];
                rebuilt[i] = activeSel;
                s_CurrentIndexField.SetValue(activeSel, i);
                s_EnableCalledField.SetValue(activeSel, true);
            }

            s_SelectablesField.SetValue(null, rebuilt);
            s_SelectableCountField.SetValue(null, activeList.Count);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SelectableRegistrySanitizer] Rebuild skipped ({reason ?? "unknown"}): {ex.Message}");
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        RebuildRegistryIfNeeded(forceRebuild: true, reason: "BeforeSceneLoad");

        if (!s_sceneLoadedSubscribed)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            s_sceneLoadedSubscribed = true;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        RebuildRegistryIfNeeded(forceRebuild: false, reason: "AfterSceneLoad");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebuildRegistryIfNeeded(forceRebuild: false, reason: "SceneLoaded");
    }

    private static int NextPowerOfTwo(int value)
    {
        int power = 16;
        while (power < value && power < (1 << 20))
        {
            power <<= 1;
        }
        return Mathf.Max(power, value);
    }
}
