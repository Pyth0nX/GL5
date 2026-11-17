using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages the project's graphics quality levels (Low, Medium, High).
/// Reads profiles configured in QualitySettings.names, applies the selected level
/// via QualitySettings.SetQualityLevel, and persists the preference in PlayerPrefs.
/// </summary>
public class GraphicsSettingsManager : MonoBehaviour
{
    public const string PrefKeyQualityLevel = "Setting_GraphicsQualityLevel";
    public const string EventQualityLevelChanged = "Settings_GraphicsQualityChanged";

    private const string DefaultQualityName = "Medium";

    public static GraphicsSettingsManager Instance { get; private set; }

    [Header("Optional: Direct Dropdown (if not using SettingsPanelUI)")]
    [SerializeField] private TMP_Dropdown _qualityDropdown;

    private CharacterPerformanceOptimizer _performanceOptimizer;

    /// <summary>
    /// Currently active graphics quality index.
    /// </summary>
    public int CurrentQualityIndex { get; private set; }

    /// <summary>
    /// Level names configured in ProjectSettings/QualitySettings.
    /// </summary>
    public string[] AvailableQualityNames => QualitySettings.names;

    /// <summary>
    /// Event invoked whenever the graphics quality level changes.
    /// </summary>
    public event Action<int> OnQualityLevelChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsurePerformanceOptimizer();
            LoadAndApplySavedQuality();
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        if (_qualityDropdown != null)
        {
            BindDropdown(_qualityDropdown);
        }

        EnsurePerformanceOptimizer();
        _performanceOptimizer.RefreshAndApply(CurrentQualityIndex);
    }

    private void OnDestroy()
    {
        if (_qualityDropdown != null)
        {
            _qualityDropdown.onValueChanged.RemoveListener(SetQualityLevel);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Loads the saved preference from PlayerPrefs (or the default "Medium" level)
    /// and applies it immediately on game startup.
    /// </summary>
    public void LoadAndApplySavedQuality()
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
        {
            CurrentQualityIndex = 0;
            return;
        }

        int defaultIndex = ResolveDefaultQualityIndex(names);
        int savedIndex = PlayerPrefs.GetInt(PrefKeyQualityLevel, defaultIndex);
        int clampedIndex = Mathf.Clamp(savedIndex, 0, names.Length - 1);

        ApplyQualityLevel(clampedIndex, savePreference: !PlayerPrefs.HasKey(PrefKeyQualityLevel));
    }

    /// <summary>
    /// Changes the graphics quality level, applies expensive runtime changes,
    /// and saves the player's selection to PlayerPrefs.
    /// </summary>
    /// <param name="index">Level index within QualitySettings.names.</param>
    public void SetQualityLevel(int index)
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
        {
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, names.Length - 1);
        ApplyQualityLevel(clampedIndex, savePreference: true);
    }

    /// <summary>
    /// Populates a TMP_Dropdown with the levels from QualitySettings.names and syncs it
    /// with the active level without firing redundant notifications.
    /// </summary>
    public void PopulateDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        string[] names = QualitySettings.names;
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            options.Add(new TMP_Dropdown.OptionData(names[i]));
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(options);

        int activeIndex = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, names.Length - 1));
        CurrentQualityIndex = activeIndex;
        dropdown.SetValueWithoutNotify(activeIndex);
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// Binds a TMP_Dropdown directly to this manager to populate its options
    /// and listen for user selection changes.
    /// </summary>
    public void BindDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        PopulateDropdown(dropdown);
        dropdown.onValueChanged.RemoveListener(SetQualityLevel);
        dropdown.onValueChanged.AddListener(SetQualityLevel);
    }

    private void ApplyQualityLevel(int index, bool savePreference)
    {
        // applyExpensiveChanges = true ensures the URP asset, shadows, MSAA, and mipmaps update
        QualitySettings.SetQualityLevel(index, true);
        CurrentQualityIndex = index;

        if (savePreference)
        {
            PlayerPrefs.SetInt(PrefKeyQualityLevel, index);
            PlayerPrefs.Save();
        }

        if (_qualityDropdown != null)
        {
            _qualityDropdown.SetValueWithoutNotify(index);
            _qualityDropdown.RefreshShownValue();
        }

        EnsurePerformanceOptimizer();
        _performanceOptimizer.HandleQualityChanged(index);

        OnQualityLevelChanged?.Invoke(index);
        EventBus.Instance?.Publish(EventQualityLevelChanged, index);
    }

    private void EnsurePerformanceOptimizer()
    {
        if (_performanceOptimizer == null)
        {
            _performanceOptimizer = GetComponent<CharacterPerformanceOptimizer>();
            if (_performanceOptimizer == null)
            {
                _performanceOptimizer = gameObject.AddComponent<CharacterPerformanceOptimizer>();
            }
        }
    }

    private static int ResolveDefaultQualityIndex(string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], DefaultQualityName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1);
    }
}
