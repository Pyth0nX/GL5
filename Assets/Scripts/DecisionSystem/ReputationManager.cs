using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Qualitative categories for protagonist Reputation / Safety across five progressive levels
/// (two negative, one neutral, and two positive).
/// </summary>
[Serializable]
public enum ReputationTier
{
    VeryLow = 0,  // Very Low / Critical: severe vulnerability and intense anxiety
    Low = 1,      // Low / Unsafe: elevated vulnerability in bullying situations
    Neutral = 2,  // Neutral: balanced intermediate / initial state
    High = 3,     // High / Safe: supportive environment and growing confidence
    VeryHigh = 4  // Very High / Secure: strong support network and high confidence
}

/// <summary>
/// Operations applicable to the Reputation / Safety metric from narrative nodes and decisions.
/// None = 0 ensures full backwards compatibility with existing JSON files.
/// </summary>
[Serializable]
public enum ReputationOperation
{
    None = 0,
    Add = 1,
    Subtract = 2,
    Set = 3
}

/// <summary>
/// Global manager for the protagonist's "Reputation / Safety" metric.
/// Automatically synchronizes its state with DecisionDataManager and EventBus,
/// and raises C# events and UnityEvents to decouple UI, post-processing, and world systems.
/// </summary>
public class ReputationManager : MonoBehaviour
{
    public const string DefaultDecisionVariableName = "Reputation";

    // Standard events published on EventBus
    public const string EventReputationChanged = "Reputation_Changed";
    public const string EventTierChanged = "Reputation_Tier_Changed";
    public const string EventTierVeryLow = "Reputation_Tier_VeryLow";
    public const string EventTierLow = "Reputation_Tier_Low";
    public const string EventTierNeutral = "Reputation_Tier_Neutral";
    public const string EventTierHigh = "Reputation_Tier_High";
    public const string EventTierVeryHigh = "Reputation_Tier_VeryHigh";

    private static ReputationManager _instance;
    private static bool _applicationIsQuitting = false;

    public static ReputationManager Instance
    {
        get
        {
            if (_instance == null && !_applicationIsQuitting)
            {
                _instance = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
                if (_instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("ReputationManager");
                    _instance = go.AddComponent<ReputationManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Reputation / Safety Bounds")]
    [Tooltip("Minimum allowed value for the Reputation / Safety metric.")]
    [SerializeField] private int _minReputation = 0;

    [Tooltip("Maximum allowed value for the Reputation / Safety metric.")]
    [SerializeField] private int _maxReputation = 100;

    [Tooltip("Initial value when starting a new game.")]
    [SerializeField] private int _initialReputation = 50;

    [Header("Categorical Thresholds (5 Tiers)")]
    [Tooltip("Values less than or equal to this threshold are classified as VeryLow.")]
    [SerializeField] private int _veryLowTierMaxThreshold = 20;

    [Tooltip("Values above VeryLow and less than or equal to this threshold are classified as Low.")]
    [SerializeField] private int _lowTierMaxThreshold = 40;

    [Tooltip("Values greater than or equal to this threshold (and below VeryHigh) are classified as High.")]
    [SerializeField] private int _highTierMinThreshold = 60;

    [Tooltip("Values greater than or equal to this threshold are classified as VeryHigh.")]
    [SerializeField] private int _veryHighTierMinThreshold = 80;

    [Header("Runtime State (Debug)")]
    [Tooltip("Current runtime value.")]
    [SerializeField] private int _currentReputation = 50;

    [Tooltip("Current category calculated based on configured thresholds.")]
    [SerializeField] private ReputationTier _currentTier = ReputationTier.Neutral;

    [Header("DecisionDataManager Integration")]
    [Tooltip("Name of the integer variable automatically synchronized in DecisionDataManager.")]
    [SerializeField] private string _decisionVariableName = DefaultDecisionVariableName;

    [Header("UnityEvents (UI & Inspector Listeners)")]
    [Tooltip("Fired when the numeric reputation value changes (sends the current value).")]
    [SerializeField] private UnityEvent<int> _onReputationChangedEvent = new UnityEvent<int>();

    [Tooltip("Fired when the reputation value changes (sends the normalized 0 to 1 value for UI bars).")]
    [SerializeField] private UnityEvent<float> _onReputationNormalizedChangedEvent = new UnityEvent<float>();

    [Tooltip("Fired only when the player crosses into a new tier (VeryLow, Low, Neutral, High, VeryHigh).")]
    [SerializeField] private UnityEvent<ReputationTier> _onReputationTierChangedEvent = new UnityEvent<ReputationTier>();

    #region C# Events
    /// <summary>
    /// C# event invoked whenever the numeric Reputation / Safety value changes.
    /// Parameter: new current value.
    /// </summary>
    public event Action<int> OnReputationChanged;

    /// <summary>
    /// Detailed C# event invoked when the value changes.
    /// Parameters: (previousValue, newValue).
    /// </summary>
    public event Action<int, int> OnReputationUpdated;

    /// <summary>
    /// C# event invoked when the categorical tier changes (VeryLow, Low, Neutral, High, VeryHigh).
    /// </summary>
    public event Action<ReputationTier> OnReputationTierChanged;
    #endregion

    #region Properties
    public int CurrentReputation => _currentReputation;
    public int MinReputation => _minReputation;
    public int MaxReputation => _maxReputation;
    public int InitialReputation => _initialReputation;
    public int VeryLowTierMaxThreshold => _veryLowTierMaxThreshold;
    public int LowTierMaxThreshold => _lowTierMaxThreshold;
    public int HighTierMinThreshold => _highTierMinThreshold;
    public int VeryHighTierMinThreshold => _veryHighTierMinThreshold;
    public ReputationTier CurrentTier => _currentTier;

    /// <summary>
    /// Normalized value in the [0, 1] range, useful for Sliders or Image.fillAmount in UI.
    /// </summary>
    public float NormalizedReputation
    {
        get
        {
            if (_maxReputation <= _minReputation) return 0f;
            return Mathf.InverseLerp(_minReputation, _maxReputation, _currentReputation);
        }
    }
    #endregion

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            ValidateConfiguration();
            _currentReputation = Mathf.Clamp(_initialReputation, _minReputation, _maxReputation);
            _currentTier = EvaluateTierForValue(_currentReputation);
            SyncDecisionVariable();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Emit initial state to UI and visual listeners subscribed via Inspector
        _onReputationChangedEvent?.Invoke(_currentReputation);
        _onReputationNormalizedChangedEvent?.Invoke(NormalizedReputation);
        _onReputationTierChangedEvent?.Invoke(_currentTier);
    }

    private void OnValidate()
    {
        ValidateConfiguration();
        _currentReputation = Mathf.Clamp(_currentReputation, _minReputation, _maxReputation);
        _currentTier = EvaluateTierForValue(_currentReputation);
    }

    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    private void ValidateConfiguration()
    {
        if (_maxReputation < _minReputation)
        {
            _maxReputation = _minReputation;
        }

        _initialReputation = Mathf.Clamp(_initialReputation, _minReputation, _maxReputation);
        _veryLowTierMaxThreshold = Mathf.Clamp(_veryLowTierMaxThreshold, _minReputation, _maxReputation);
        _lowTierMaxThreshold = Mathf.Clamp(_lowTierMaxThreshold, _veryLowTierMaxThreshold, _maxReputation);
        _highTierMinThreshold = Mathf.Clamp(_highTierMinThreshold, _lowTierMaxThreshold, _maxReputation);
        _veryHighTierMinThreshold = Mathf.Clamp(_veryHighTierMinThreshold, _highTierMinThreshold, _maxReputation);

        if (string.IsNullOrWhiteSpace(_decisionVariableName))
        {
            _decisionVariableName = DefaultDecisionVariableName;
        }
    }

    /// <summary>
    /// Evaluates which categorical tier (VeryLow, Low, Neutral, High, VeryHigh) a given numerical value belongs to.
    /// </summary>
    public ReputationTier EvaluateTierForValue(int value)
    {
        if (value <= _veryLowTierMaxThreshold)
        {
            return ReputationTier.VeryLow;
        }
        if (value <= _lowTierMaxThreshold)
        {
            return ReputationTier.Low;
        }
        if (value >= _veryHighTierMinThreshold)
        {
            return ReputationTier.VeryHigh;
        }
        if (value >= _highTierMinThreshold)
        {
            return ReputationTier.High;
        }
        return ReputationTier.Neutral;
    }

    /// <summary>
    /// Applies a narrative operation (Add, Subtract, Set) to the current reputation.
    /// </summary>
    public void ApplyOperation(ReputationOperation operation, int amount)
    {
        switch (operation)
        {
            case ReputationOperation.None:
                break;
            case ReputationOperation.Add:
                AddReputation(amount);
                break;
            case ReputationOperation.Subtract:
                SubtractReputation(amount);
                break;
            case ReputationOperation.Set:
                SetReputation(amount);
                break;
        }
    }

    /// <summary>
    /// Adds (or subtracts if negative) the specified amount to/from the current Reputation / Safety.
    /// </summary>
    public void AddReputation(int delta)
    {
        SetReputation(_currentReputation + delta);
    }

    /// <summary>
    /// Subtracts the specified amount from the current Reputation / Safety.
    /// </summary>
    public void SubtractReputation(int amount)
    {
        SetReputation(_currentReputation - amount);
    }

    /// <summary>
    /// Sets the Reputation / Safety to an exact value within [Min, Max] bounds.
    /// </summary>
    public void SetReputation(int newValue)
    {
        int clampedValue = Mathf.Clamp(newValue, _minReputation, _maxReputation);
        int previousValue = _currentReputation;
        ReputationTier previousTier = _currentTier;

        if (clampedValue == previousValue)
        {
            return;
        }

        _currentReputation = clampedValue;
        _currentTier = EvaluateTierForValue(_currentReputation);

        SyncDecisionVariable();

        Debug.Log($"[ReputationManager] Reputation/Safety updated: {previousValue} -> {_currentReputation} (Tier: {_currentTier})");

        // 1. Notify C# code subscribers
        OnReputationChanged?.Invoke(_currentReputation);
        OnReputationUpdated?.Invoke(previousValue, _currentReputation);

        // 2. Notify serialized UnityEvents (UI / Inspector)
        _onReputationChangedEvent?.Invoke(_currentReputation);
        _onReputationNormalizedChangedEvent?.Invoke(NormalizedReputation);

        // 3. Publish to project global EventBus
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(EventReputationChanged);
            EventBus.Instance.Publish<int>(EventReputationChanged, _currentReputation);
        }

        // 4. If a categorical threshold was crossed, fire tier change events
        if (_currentTier != previousTier)
        {
            Debug.Log($"[ReputationManager] Safety tier changed: {previousTier} -> {_currentTier}");
            OnReputationTierChanged?.Invoke(_currentTier);
            _onReputationTierChangedEvent?.Invoke(_currentTier);
            PublishTierEventsToEventBus(_currentTier);
        }
    }

    /// <summary>
    /// Resets Reputation / Safety to its configured initial value (e.g. when returning to Main Menu).
    /// </summary>
    public void ResetReputation()
    {
        SetReputation(_initialReputation);
        SyncDecisionVariable();
    }

    /// <summary>
    /// Restores Reputation / Safety from a saved game and refreshes all UI and DecisionDataManager listeners.
    /// </summary>
    public void RestoreReputation(int savedValue)
    {
        int clampedValue = Mathf.Clamp(savedValue, _minReputation, _maxReputation);
        int previousValue = _currentReputation;
        ReputationTier previousTier = _currentTier;

        _currentReputation = clampedValue;
        _currentTier = EvaluateTierForValue(_currentReputation);
        SyncDecisionVariable();

        OnReputationChanged?.Invoke(_currentReputation);
        if (previousValue != _currentReputation)
        {
            OnReputationUpdated?.Invoke(previousValue, _currentReputation);
        }

        _onReputationChangedEvent?.Invoke(_currentReputation);
        _onReputationNormalizedChangedEvent?.Invoke(NormalizedReputation);

        if (_currentTier != previousTier)
        {
            OnReputationTierChanged?.Invoke(_currentTier);
            _onReputationTierChangedEvent?.Invoke(_currentTier);
            PublishTierEventsToEventBus(_currentTier);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(EventReputationChanged);
            EventBus.Instance.Publish<int>(EventReputationChanged, _currentReputation);
        }
    }

    private void PublishTierEventsToEventBus(ReputationTier tier)
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Publish(EventTierChanged);
        EventBus.Instance.Publish<ReputationTier>(EventTierChanged, tier);

        string tierEvent = tier switch
        {
            ReputationTier.VeryLow => EventTierVeryLow,
            ReputationTier.Low => EventTierLow,
            ReputationTier.High => EventTierHigh,
            ReputationTier.VeryHigh => EventTierVeryHigh,
            _ => EventTierNeutral
        };
        EventBus.Instance.Publish(tierEvent);
    }

    private void SyncDecisionVariable()
    {
        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.SetOrAddIntVariable(
                _decisionVariableName,
                _currentReputation,
                "Global protagonist Reputation / Safety metric");
        }
    }

    #region Inspector Debug Helpers
    [ContextMenu("Debug: +10 Reputation")]
    private void DebugAddTen() => AddReputation(10);

    [ContextMenu("Debug: -10 Reputation")]
    private void DebugSubtractTen() => SubtractReputation(10);

    [ContextMenu("Debug: Set Tier VeryLow")]
    private void DebugSetVeryLow() => SetReputation(_minReputation);

    [ContextMenu("Debug: Set Tier Low")]
    private void DebugSetLow() => SetReputation((_veryLowTierMaxThreshold + _lowTierMaxThreshold + 1) / 2);

    [ContextMenu("Debug: Set Tier Neutral")]
    private void DebugSetNeutral() => SetReputation((_lowTierMaxThreshold + _highTierMinThreshold) / 2);

    [ContextMenu("Debug: Set Tier High")]
    private void DebugSetHigh() => SetReputation((_highTierMinThreshold + _veryHighTierMinThreshold) / 2);

    [ContextMenu("Debug: Set Tier VeryHigh")]
    private void DebugSetVeryHigh() => SetReputation(_maxReputation);

    [ContextMenu("Debug: Reset Reputation")]
    private void DebugReset() => ResetReputation();
    #endregion
}
