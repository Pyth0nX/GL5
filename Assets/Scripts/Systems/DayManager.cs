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
        if (clampedDay == 1)
        {
            TransitionManager.Instance?.ResetCutsceneState();
        }

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
                "Connect", 1, 0, RouteNode(route, 1, 2, 3));
            MessagesManager.Instance.AppendTestSkipHistory(
                "Alex", 1, 9, RouteNode(route, 10, 11, 12), 13);
            MessagesManager.Instance.AppendTestSkipHistory("Jamie", 1, 14);
        }

        if (targetDay > 2)
        {
            MessagesManager.Instance.AppendTestSkipHistory("Alex", 2, 15);
            MessagesManager.Instance.AppendTestSkipHistory(
                "Jamie", 2,
                20,
                RouteNode(route, 21, 22, 23),
                24,
                RouteNode(route, 25, 26, 27));
            MessagesManager.Instance.AppendTestSkipHistory("Noah", 2, 28);
        }

        if (targetDay > 3)
        {
            if (route == TestRoute.Safe)
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 29, 31, 32, 33, 36, 37);
            }
            else if (route == TestRoute.Neutral)
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 29, 31, 32, 34, 36, 38, 40, 42);
            }
            else
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 29, 31, 32, 35, 36, 39, 40, 43);
            }

            MessagesManager.Instance.AppendTestSkipHistory("Unknown", 3, 44);
        }

        if (targetDay > 4)
        {
            MessagesManager.Instance.AppendTestSkipHistory(
                "Unknown", 4,
                45,
                47,
                48,
                RouteNode(route, 49, 50, 51),
                52,
                RouteNode(route, 53, 54, 55));
            MessagesManager.Instance.AppendTestSkipHistory("Riley", 4, 56);
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
                SetContactState("Student Group", 75);
                SetContactState("Connect", RouteNode(route, 1, 2, 3));
                SetContactState("Emma", 8);
                SetContactState("Alex", 13);
                SetContactState("Jamie", 14);
                break;

            case 2:
                SetContactState("Alex", 15);
                SetContactState("Emma", RouteNode(route, 17, 18, 19));
                SetContactState("Jamie", RouteNode(route, 25, 26, 27));
                SetContactState("Noah", 28);
                break;

            case 3:
                SetContactState("Emma", 30);
                SetContactState("Noah", RouteNode(route, 37, 42, 43));
                SetContactState("Unknown", 44);
                break;

            case 4:
                SetContactState("Emma", 46);
                SetContactState("Unknown", RouteNode(route, 53, 54, 55));
                SetContactState("Riley", 56);
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
        int endingNode = finalScore <= -4 ? 71 : finalScore >= 5 ? 73 : 72;

        Debug.Log($"[DayManager] Resolving campaign ending with score {finalScore}; node {endingNode}.");
        MessagesManager.Instance?.AdvanceContactNarrativeNodeByEvent("Riley", endingNode);
    }

    #endregion
}
