using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DayManager : MonoBehaviour
{
    public enum TestRoute
    {
        Safe,
        Neutral,
        Risky
    }

    public static DayManager Instance { get; private set; }

    public const int ChoiceReputationStep = 6;
    public const int DefaultNeutralReputation = 50;
    public const int Day1DebugSafeReputation = 70;
    public const int Day1DebugRiskyReputation = 30;

    private static bool _hasQueuedDebugStart;
    private static int _queuedDebugDay = 1;
    private static TestRoute _queuedDebugRoute = TestRoute.Neutral;

    public static bool HasQueuedDebugStart => _hasQueuedDebugStart;
    public static int QueuedDebugDay => _queuedDebugDay;
    public static TestRoute QueuedDebugRoute => _queuedDebugRoute;
    public static bool ShouldSkipSceneOneOpening => _hasQueuedDebugStart && _queuedDebugDay > 1;

    [SerializeField] private int _currentDay = 0;

    private int _riskScore;
    private readonly Dictionary<string, Action> _choiceSubscriptions = new Dictionary<string, Action>();
    private readonly HashSet<int> _skippedDays = new HashSet<int>();

    public int NarrativeRiskScore => _riskScore;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("NextDay", OnNextDay);
            RegisterNarrativeChoices();
            EventBus.Instance.Subscribe("ResolveEnding_Safe", ResolveSafeEnding);
            EventBus.Instance.Subscribe("ResolveEnding_Neutral", ResolveNeutralEnding);
            EventBus.Instance.Subscribe("ResolveEnding_Risky", ResolveRiskyEnding);
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("NextDay", OnNextDay);
            EventBus.Instance.Unsubscribe("ResolveEnding_Safe", ResolveSafeEnding);
            EventBus.Instance.Unsubscribe("ResolveEnding_Neutral", ResolveNeutralEnding);
            EventBus.Instance.Unsubscribe("ResolveEnding_Risky", ResolveRiskyEnding);

            foreach (KeyValuePair<string, Action> subscription in _choiceSubscriptions)
            {
                EventBus.Instance.Unsubscribe(subscription.Key, subscription.Value);
            }
        }

        _choiceSubscriptions.Clear();
    }

    public void ResetDay()
    {
        _currentDay = 0;
        _riskScore = 0;
        _skippedDays.Clear();
    }

    /// <summary>
    /// Directly sets the current day when restoring a saved game without re-triggering day start events.
    /// </summary>
    public void SetCurrentDay(int day)
    {
        _currentDay = Mathf.Max(0, day);
    }

    public void SetNarrativeRiskScore(int riskScore)
    {
        _riskScore = riskScore;
    }

    private void OnNextDay()
    {
        _currentDay++;
        Debug.Log($"[DayManager] Advanced to Day {_currentDay}");

        // Broadcast the specific day start event so EventNodeUpdater and scene objects can react
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish($"Start_Day{_currentDay}");
        }
    }

    public int GetCurrentDay()
    {
        return _currentDay;
    }

    public bool TryMarkDaySkipped(int day)
    {
        return _skippedDays.Add(day);
    }

    public bool WasDaySkipped(int day)
    {
        return _skippedDays.Contains(day);
    }

    #region Debug Campaign Start & Profile Simulation

    /// <summary>
    /// Computes the exact Reputation score resulting from starting at the specified day with the given previous choices profile.
    /// </summary>
    public static int CalculateProfileReputation(int targetDay, TestRoute route)
    {
        int clampedDay = Mathf.Clamp(targetDay, 1, 5);
        int previousChoices = GetLastChoiceBeforeDay(clampedDay);

        if (previousChoices <= 0)
        {
            return route switch
            {
                TestRoute.Safe => Day1DebugSafeReputation,
                TestRoute.Risky => Day1DebugRiskyReputation,
                _ => DefaultNeutralReputation
            };
        }

        int deltaPerChoice = route switch
        {
            TestRoute.Safe => ChoiceReputationStep,
            TestRoute.Risky => -ChoiceReputationStep,
            _ => 0
        };

        return Mathf.Clamp(DefaultNeutralReputation + (previousChoices * deltaPerChoice), 0, 100);
    }

    public static int GetOpeningObjectiveId(int day)
    {
        return day switch
        {
            2 => 8,
            3 => 16,
            4 => 23,
            5 => 31,
            _ => 0
        };
    }

    public static int GetLastChoiceBeforeDay(int day)
    {
        return day switch
        {
            2 => 2,
            3 => 5,
            4 => 8,
            5 => 10,
            _ => 0
        };
    }

    public static void QueueTestStart(int day, TestRoute route)
    {
        _queuedDebugDay = Mathf.Clamp(day, 1, 5);
        _queuedDebugRoute = route;
        _hasQueuedDebugStart = true;
    }

    public static void ClearQueuedTestStart()
    {
        _hasQueuedDebugStart = false;
        _queuedDebugDay = 1;
        _queuedDebugRoute = TestRoute.Neutral;
    }

    /// <summary>
    /// Starts or restarts a campaign session directly on the selected day (1..5) with simulated prior choices
    /// and matching Reputation / Anxiety visual state. Safe to invoke from Main Menu or Pause Menu.
    /// </summary>
    public void StartDebugSession(int day, TestRoute route)
    {
        int clampedDay = Mathf.Clamp(day, 1, 5);
        QueueTestStart(clampedDay, route);

        Time.timeScale = 1f;

        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.DetachActiveSlotForDebugSession();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetHasEnteredHomeFirstTime(false);
        }

        ResetDay();
        _currentDay = clampedDay;

        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.ResetVariables();
        }

        if (MessagesManager.Instance != null)
        {
            MessagesManager.Instance.ResetToInitialState();
        }

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.ResetObjectives();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ResetConnectState();
        }

        ReputationManager reputationManager = ReputationManager.Instance != null
            ? ReputationManager.Instance
            : FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (reputationManager != null)
        {
            reputationManager.SetReputation(CalculateProfileReputation(clampedDay, route));
        }

        Debug.Log($"[DayManager] Launching Debug Session -> Day {clampedDay}, Profile: {route} (Reputation: {CalculateProfileReputation(clampedDay, route)}).");
        SceneManager.LoadScene("Home");
    }

    /// <summary>
    /// Applies the queued debug day, previous decision history, Connect app setup, objective, and Reputation value
    /// once the Home scene has initialized its listeners.
    /// </summary>
    public void ApplyQueuedDebugStart()
    {
        if (!_hasQueuedDebugStart)
        {
            return;
        }

        int targetDay = Mathf.Clamp(_queuedDebugDay, 1, 5);
        TestRoute route = _queuedDebugRoute;

        _currentDay = targetDay;
        _riskScore = 0;
        _skippedDays.Clear();

        if (targetDay >= 2)
        {
            UIManager.Instance?.ApplyCompletedConnectSetupForTestSkip();
            RebuildTestSkipHistories(targetDay, route);

            for (int completedDay = 1; completedDay < targetDay; completedDay++)
            {
                ApplyCompletedDayContactState(completedDay, route);
            }
        }
        else
        {
            UIManager.Instance?.ResetConnectState();
        }

        int objectiveId = GetOpeningObjectiveId(targetDay);
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.ForceSetObjective(objectiveId);
        }
        else if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish("ForceSetObjective", objectiveId);
        }

        int lastPreviousChoice = GetLastChoiceBeforeDay(targetDay);
        string routeName = route.ToString();
        if (EventBus.Instance != null)
        {
            for (int choice = 1; choice <= lastPreviousChoice; choice++)
            {
                EventBus.Instance.Publish($"Choice{choice}_{routeName}");
            }
        }

        int targetReputation = CalculateProfileReputation(targetDay, route);
        ReputationManager reputationManager = ReputationManager.Instance != null
            ? ReputationManager.Instance
            : FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (reputationManager != null)
        {
            reputationManager.SetReputation(targetReputation);
        }

        ClearQueuedTestStart();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish($"Start_Day{_currentDay}");
        }

        Debug.Log(
            $"[DayManager] Debug start active on Day {targetDay}: " +
            $"Choices 1-{lastPreviousChoice} = {routeName}, RiskScore = {_riskScore}, Reputation = {targetReputation}.");
    }

    private static void RebuildTestSkipHistories(int targetDay, TestRoute route)
    {
        if (MessagesManager.Instance == null) return;

        MessagesManager.Instance.BeginTestSkipHistoryRebuild();

        if (targetDay > 1)
        {
            MessagesManager.Instance.AppendTestSkipHistory(
                "Connect", 1, 1000, RouteNode(route, 1001, 1002, 1003));
            MessagesManager.Instance.AppendTestSkipHistory(
                "Alex", 1, 1200, RouteNode(route, 1201, 1202, 1203), 1210);
            MessagesManager.Instance.AppendTestSkipHistory("Jamie", 1, 1220);
        }

        if (targetDay > 2)
        {
            MessagesManager.Instance.AppendTestSkipHistory("Alex", 2, 1300);
            MessagesManager.Instance.AppendTestSkipHistory(
                "Jamie", 2,
                1500,
                RouteNode(route, 1501, 1502, 1503),
                1510,
                RouteNode(route, 1511, 1512, 1513));
            MessagesManager.Instance.AppendTestSkipHistory("Noah", 2, 1600);
        }

        if (targetDay > 3)
        {
            if (route == TestRoute.Safe)
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 1700, 1850, 1900, 1901, 1904, 1905);
            }
            else if (route == TestRoute.Neutral)
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 1700, 1850, 1900, 1902, 1904, 1906, 1910, 1912);
            }
            else
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 1700, 1850, 1900, 1903, 1904, 1907, 1910, 1913);
            }

            MessagesManager.Instance.AppendTestSkipHistory("Unknown", 3, 1950);
        }

        if (targetDay > 4)
        {
            MessagesManager.Instance.AppendTestSkipHistory(
                "Unknown", 4,
                2000,
                2150,
                2200,
                RouteNode(route, 2201, 2202, 2203),
                2210,
                RouteNode(route, 2211, 2212, 2213));
            MessagesManager.Instance.AppendTestSkipHistory("Riley", 4, 2300);
        }

        MessagesManager.Instance.CompleteTestSkipHistoryRebuild();
    }

    private static int RouteNode(TestRoute route, int safeNode, int neutralNode, int riskyNode)
    {
        return route switch
        {
            TestRoute.Safe => safeNode,
            TestRoute.Risky => riskyNode,
            _ => neutralNode
        };
    }

    private static void ApplyCompletedDayContactState(int day, TestRoute route)
    {
        if (MessagesManager.Instance == null) return;

        switch (day)
        {
            case 1:
                SetContactState("Student Group", 2801);
                SetContactState("Connect", RouteNode(route, 1001, 1002, 1003));
                SetContactState("Emma", 1100);
                SetContactState("Alex", 1210);
                SetContactState("Jamie", 1220);
                break;

            case 2:
                SetContactState("Alex", 1300);
                SetContactState("Emma", RouteNode(route, 1401, 1402, 1403));
                SetContactState("Jamie", RouteNode(route, 1511, 1512, 1513));
                SetContactState("Noah", 1600);
                break;

            case 3:
                SetContactState("Emma", 1800);
                SetContactState("Noah", RouteNode(route, 1905, 1912, 1913));
                SetContactState("Unknown", 1950);
                break;

            case 4:
                SetContactState("Emma", 2100);
                SetContactState("Unknown", RouteNode(route, 2211, 2212, 2213));
                SetContactState("Riley", 2300);
                break;
        }
    }

    private static void SetContactState(string contactName, int narrativeNodeId)
    {
        MessagesManager.Instance.SetContactNarrativeStateSilently(contactName, narrativeNodeId);
    }

    #endregion

    #region Narrative Choice Tracking & Ending Resolution

    private void RegisterNarrativeChoices()
    {
        for (int choice = 1; choice <= 13; choice++)
        {
            SubscribeChoice($"Choice{choice}_Safe", -1);
            SubscribeChoice($"Choice{choice}_Neutral", 0);
            SubscribeChoice($"Choice{choice}_Risky", 1);
        }
    }

    private void SubscribeChoice(string eventName, int riskDelta)
    {
        Action action = () =>
        {
            _riskScore += riskDelta;
            Debug.Log($"[DayManager] Recorded {eventName}. Current narrative risk score: {_riskScore}.");
        };

        _choiceSubscriptions[eventName] = action;
        EventBus.Instance.Subscribe(eventName, action);
    }

    private void ResolveSafeEnding() => StartCoroutine(ResolveEndingNextFrame(-3));
    private void ResolveNeutralEnding() => StartCoroutine(ResolveEndingNextFrame(0));
    private void ResolveRiskyEnding() => StartCoroutine(ResolveEndingNextFrame(3));

    private IEnumerator ResolveEndingNextFrame(int finalDecisionModifier)
    {
        // DialogueButton finishes the selected branch immediately after publishing its end event.
        // Waiting one frame prevents that cleanup from overwriting the ending node selected here.
        yield return null;

        int finalScore = _riskScore + finalDecisionModifier;
        int endingNode = finalScore <= -4 ? 2700 : finalScore >= 5 ? 2702 : 2701;

        Debug.Log($"[DayManager] Resolving campaign ending with score {finalScore}; node {endingNode}.");
        MessagesManager.Instance?.AdvanceContactNarrativeNodeByEvent("Riley", endingNode);
    }

    #endregion
}
