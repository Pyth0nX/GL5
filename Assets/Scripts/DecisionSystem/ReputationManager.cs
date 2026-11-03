using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Qualitative categories for protagonist Reputation / Safety.
/// </summary>
[Serializable]
public enum ReputationTier
{
    Low_Unsafe = 0, // Low / Unsafe: higher vulnerability in bullying situations
    Neutral = 1,    // Neutral: intermediate / initial state
    High_Safe = 2   // High / Safe: strong support network and higher confidence
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
/// and raises C# events and UnityEvents to decouple UI and world systems.
/// </summary>
public class ReputationManager : MonoBehaviour
{
    public const string DefaultDecisionVariableName = "Reputation";

    // Standard events published on EventBus
    public const string EventReputationChanged = "Reputation_Changed";
    public const string EventTierLow = "Reputation_Tier_Low";
    public const string EventTierNeutral = "Reputation_Tier_Neutral";
    public const string EventTierHigh = "Reputation_Tier_High";

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

    [Header("Categorical Thresholds (Tiers)")]
    [Tooltip("Values less than or equal to this threshold are classified as Low_Unsafe.")]
    [SerializeField] private int _lowTierMaxThreshold = 33;

    [Tooltip("Values greater than or equal to this threshold are classified as High_Safe.")]
    [SerializeField] private int _highTierMinThreshold = 67;

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

    [Tooltip("Fired only when the player crosses into a new tier (Low_Unsafe, Neutral, High_Safe).")]
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
    /// C# event invoked when the categorical tier changes (Low_Unsafe, Neutral, High_Safe).
    /// </summary>
    public event Action<ReputationTier> OnReputationTierChanged;
    #endregion

    #region Properties
    public int CurrentReputation => _currentReputation;
    public int MinReputation => _minReputation;
    public int MaxReputation => _maxReputation;
    public int InitialReputation => _initialReputation;
    public int LowTierMaxThreshold => _lowTierMaxThreshold;
    public int HighTierMinThreshold => _highTierMinThreshold;
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
        // Emit initial state to UI listeners subscribed via Inspector
        _onReputationChangedEvent?.Invoke(_currentReputation);
        _onReputationNormalizedChangedEvent?.Invoke(NormalizedReputation);
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
        _lowTierMaxThreshold = Mathf.Clamp(_lowTierMaxThreshold, _minReputation, _maxReputation);
        _highTierMinThreshold = Mathf.Clamp(_highTierMinThreshold, _lowTierMaxThreshold, _maxReputation);

        if (string.IsNullOrWhiteSpace(_decisionVariableName))
        {
            _decisionVariableName = DefaultDecisionVariableName;
        }
    }

    /// <summary>
    /// Evaluates which categorical tier a given numerical value belongs to.
    /// </summary>
    public ReputationTier EvaluateTierForValue(int value)
    {
        if (value <= _lowTierMaxThreshold)
        {
            return ReputationTier.Low_Unsafe;
        }
        if (value >= _highTierMinThreshold)
        {
            return ReputationTier.High_Safe;
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

            if (EventBus.Instance != null)
            {
                string tierEvent = _currentTier switch
                {
                    ReputationTier.Low_Unsafe => EventTierLow,
                    ReputationTier.High_Safe => EventTierHigh,
                    _ => EventTierNeutral
                };
                EventBus.Instance.Publish(tierEvent);
            }
        }
    }

    /// <summary>
    /// Resets Reputation / Safety to its configured initial value (e.g. when returning to Main Menu).
    /// </summary>
    public void ResetReputation()
    {
        SetReputation(_initialReputation);
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

    [ContextMenu("Debug: Reset Reputation")]
    private void DebugReset() => ResetReputation();
    #endregion
}
