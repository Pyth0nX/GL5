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

    private readonly Dictionary<int, TestRoute> _recordedChoices = new Dictionary<int, TestRoute>();
    private int _lastResolvedEndingNode = -1;

    public int NarrativeRiskScore => _riskScore;
    public IReadOnlyDictionary<int, TestRoute> RecordedChoices => _recordedChoices;
    public int LastResolvedEndingNode => _lastResolvedEndingNode;

    /// <summary>
    /// Attempts to retrieve the route selected by the player for a specific campaign choice (1..13).
    /// Also checks DecisionDataManager for choices restored from a saved game or recorded during gameplay.
    /// </summary>
    public bool TryGetRecordedChoice(int choiceIndex, out TestRoute route)
    {
        if (_recordedChoices.TryGetValue(choiceIndex, out route))
        {
            return true;
        }

        if (DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable($"Scenario_Choice_{choiceIndex}", out int savedRoute) &&
            savedRoute >= 0 && savedRoute <= 2)
        {
            route = (TestRoute)savedRoute;
            _recordedChoices[choiceIndex] = route;
            return true;
        }

        int nodeId = GetNodeIdForChoice(choiceIndex);
        if (nodeId >= 0 && DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable($"Scenario_Node_{nodeId}", out int savedNodeOption) &&
            savedNodeOption >= 0 && savedNodeOption <= 2)
        {
            route = (TestRoute)savedNodeOption;
            _recordedChoices[choiceIndex] = route;
            return true;
        }

        if (choiceIndex == 2 && DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable("Scenario_Node_4", out int savedNodeOption4) &&
            savedNodeOption4 >= 0 && savedNodeOption4 <= 2)
        {
            route = (TestRoute)savedNodeOption4;
            _recordedChoices[choiceIndex] = route;
            return true;
        }

        route = TestRoute.Neutral;
        return false;
    }

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
        _recordedChoices.Clear();
        _lastResolvedEndingNode = -1;
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

    private static readonly Dictionary<int, TestRoute> _preservedDebugChoices = new Dictionary<int, TestRoute>();

    /// <summary>
    /// Maps a canonical campaign choice (1..13) to its primary NarrativeNode ID.
    /// </summary>
    public static int GetNodeIdForChoice(int choiceIndex)
    {
        return choiceIndex switch
        {
            1 => 0,   // Day 1 - Connect Profile
            2 => 9,   // Day 1 - Alex Chat
            3 => 16,  // Day 2 - Emma Post Reaction
            4 => 20,  // Day 2 - Jamie Post
            5 => 24,  // Day 2 - Connect Public Post
            6 => 32,  // Day 3 - Noah Age Question
            7 => 36,  // Day 3 - Noah Photo Request
            8 => 40,  // Day 3 - Noah Leaked Photo Crisis
            9 => 48,  // Day 4 - Unknown Stalker Message
            10 => 52, // Day 4 - Unknown Profile Privacy
            11 => 59, // Day 5 - Riley Rumor
            12 => 63, // Day 5 - Riley Credentials
            13 => 67, // Day 5 - Riley Final Decision
            _ => -1
        };
    }

    /// <summary>
    /// Checks whether the player has already made a specific campaign choice (1..13).
    /// </summary>
    public static bool HasPlayerMadeChoice(int choiceIndex)
    {
        if (_preservedDebugChoices.ContainsKey(choiceIndex))
        {
            return true;
        }

        return Instance != null && Instance.TryGetRecordedChoice(choiceIndex, out _);
    }

    /// <summary>
    /// Counts how many choices prior to the target day have already been decided by the player.
    /// </summary>
    public static int GetPreservedChoicesCountBeforeDay(int targetDay)
    {
        int clampedDay = Mathf.Clamp(targetDay, 1, 5);
        int lastChoice = GetLastChoiceBeforeDay(clampedDay);
        int count = 0;
        for (int c = 1; c <= lastChoice; c++)
        {
            if (HasPlayerMadeChoice(c))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Returns the effective route for a choice when performing a debug day-skip:
    /// uses the player's previously selected route if already decided, or the fallback debug route if not yet decided.
    /// </summary>
    public static TestRoute GetEffectiveChoiceRoute(int choiceIndex, TestRoute fallbackRoute)
    {
        if (_preservedDebugChoices.TryGetValue(choiceIndex, out TestRoute preserved))
        {
            return preserved;
        }

        if (Instance != null && Instance.TryGetRecordedChoice(choiceIndex, out TestRoute playerChoice))
        {
            return playerChoice;
        }

        return fallbackRoute;
    }

    /// <summary>
    /// Captures choices already made by the player prior to the target day so they survive the session reset.
    /// </summary>
    public void CaptureExistingChoicesForDebugSkip(int targetDay)
    {
        _preservedDebugChoices.Clear();
        int clampedDay = Mathf.Clamp(targetDay, 1, 5);
        int lastChoice = GetLastChoiceBeforeDay(clampedDay);

        for (int c = 1; c <= lastChoice; c++)
        {
            if (TryGetRecordedChoice(c, out TestRoute playerChoice))
            {
                _preservedDebugChoices[c] = playerChoice;
            }
        }
    }

    /// <summary>
    /// Computes the exact Reputation score resulting from starting at the specified day:
    /// preserves all choices already decided by the player, and fills any missing choices up to targetDay with the given profile route.
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

        int currentReputation = DefaultNeutralReputation;
        TestRoute effectiveRoute7 = GetEffectiveChoiceRoute(7, route);
        bool skipChoice8 = (clampedDay >= 4) && (effectiveRoute7 == TestRoute.Safe);

        for (int choice = 1; choice <= previousChoices; choice++)
        {
            if (choice == 8 && skipChoice8)
            {
                continue;
            }

            TestRoute effective = GetEffectiveChoiceRoute(choice, route);
            int delta = effective switch
            {
                TestRoute.Safe => ChoiceReputationStep,
                TestRoute.Risky => -ChoiceReputationStep,
                _ => 0
            };
            currentReputation += delta;
        }

        return Mathf.Clamp(currentReputation, 0, 100);
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
        _preservedDebugChoices.Clear();
    }

    /// <summary>
    /// Starts or restarts a campaign session directly on the selected day (1..5) with simulated prior choices
    /// and matching Reputation / Anxiety visual state. Safe to invoke from Main Menu or Pause Menu.
    /// Choices already made by the player are preserved; only unmade choices up to the selected day are filled with the chosen route.
    /// </summary>
    public void StartDebugSession(int day, TestRoute route)
    {
        int clampedDay = Mathf.Clamp(day, 1, 5);
        CaptureExistingChoicesForDebugSkip(clampedDay);
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

        int targetReputation = CalculateProfileReputation(clampedDay, route);
        ReputationManager reputationManager = ReputationManager.Instance != null
            ? ReputationManager.Instance
            : FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (reputationManager != null)
        {
            reputationManager.SetReputation(targetReputation);
        }

        Debug.Log($"[DayManager] Launching Debug Session -> Day {clampedDay}, Profile: {route} (Preserved: {_preservedDebugChoices.Count}, Reputation: {targetReputation}).");
        SceneManager.LoadScene("Home");
    }

    /// <summary>
    /// Applies the queued debug day, previous decision history, Connect app setup, objective, and Reputation value
    /// once the Home scene has initialized its listeners.
    /// Preserves choices previously taken by the player and only fills missing decisions with the chosen profile route.
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
        _recordedChoices.Clear();
        _lastResolvedEndingNode = -1;

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
        TestRoute effectiveRoute7 = GetEffectiveChoiceRoute(7, route);
        bool skipChoice8 = (targetDay >= 4) && (effectiveRoute7 == TestRoute.Safe);

        for (int choice = 1; choice <= lastPreviousChoice; choice++)
        {
            if (choice == 8 && skipChoice8)
            {
                continue;
            }

            TestRoute effectiveRoute = GetEffectiveChoiceRoute(choice, route);
            string effectiveRouteName = effectiveRoute.ToString();

            if (DecisionDataManager.Instance != null)
            {
                DecisionDataManager.Instance.SetOrAddIntVariable($"Scenario_Choice_{choice}", (int)effectiveRoute);
                int decisionNodeId = GetNodeIdForChoice(choice);
                if (decisionNodeId >= 0)
                {
                    DecisionDataManager.Instance.SetOrAddIntVariable($"Scenario_Node_{decisionNodeId}", (int)effectiveRoute);
                }
            }

            if (EventBus.Instance != null)
            {
                EventBus.Instance.Publish($"Choice{choice}_{effectiveRouteName}");
            }
            else
            {
                _recordedChoices[choice] = effectiveRoute;
                int riskDelta = effectiveRoute switch
                {
                    TestRoute.Safe => -1,
                    TestRoute.Risky => 1,
                    _ => 0
                };
                _riskScore += riskDelta;
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
            $"Choices 1-{lastPreviousChoice} applied, RiskScore = {_riskScore}, Reputation = {targetReputation}.");
    }

    private static void RebuildTestSkipHistories(int targetDay, TestRoute fallbackRoute)
    {
        if (MessagesManager.Instance == null) return;

        MessagesManager.Instance.BeginTestSkipHistoryRebuild();

        if (targetDay > 1)
        {
            TestRoute route1 = GetEffectiveChoiceRoute(1, fallbackRoute);
            TestRoute route2 = GetEffectiveChoiceRoute(2, fallbackRoute);

            MessagesManager.Instance.AppendTestSkipHistory(
                "Connect", 1, 0, RouteNode(route1, 1, 2, 3));
            MessagesManager.Instance.AppendTestSkipHistory(
                "Alex", 1, 9, RouteNode(route2, 10, 11, 12), 13);
            MessagesManager.Instance.AppendTestSkipHistory("Jamie", 1, 14);
        }

        if (targetDay > 2)
        {
            TestRoute route3 = GetEffectiveChoiceRoute(3, fallbackRoute);
            TestRoute route4 = GetEffectiveChoiceRoute(4, fallbackRoute);
            TestRoute route5 = GetEffectiveChoiceRoute(5, fallbackRoute);

            MessagesManager.Instance.AppendTestSkipHistory("Alex", 2, 15);
            MessagesManager.Instance.AppendTestSkipHistory(
                "Emma", 2, 16, RouteNode(route3, 17, 18, 19));
            MessagesManager.Instance.AppendTestSkipHistory(
                "Jamie", 2,
                20,
                RouteNode(route4, 21, 22, 23),
                24,
                RouteNode(route5, 25, 26, 27));
            MessagesManager.Instance.AppendTestSkipHistory("Noah", 2, 28);
        }

        if (targetDay > 3)
        {
            TestRoute route6 = GetEffectiveChoiceRoute(6, fallbackRoute);
            TestRoute route7 = GetEffectiveChoiceRoute(7, fallbackRoute);
            TestRoute route8 = GetEffectiveChoiceRoute(8, fallbackRoute);

            int choice6Node = RouteNode(route6, 33, 34, 35);
            int choice7Node = RouteNode(route7, 37, 38, 39);

            if (route7 == TestRoute.Safe)
            {
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 29, 31, 32, choice6Node, 36, choice7Node);
            }
            else
            {
                int choice8Node = RouteNode(route8, 41, 42, 43);
                MessagesManager.Instance.AppendTestSkipHistory(
                    "Noah", 3, 29, 31, 32, choice6Node, 36, choice7Node, 40, choice8Node);
            }

            MessagesManager.Instance.AppendTestSkipHistory("Unknown", 3, 44);
        }

        if (targetDay > 4)
        {
            TestRoute route9 = GetEffectiveChoiceRoute(9, fallbackRoute);
            TestRoute route10 = GetEffectiveChoiceRoute(10, fallbackRoute);

            MessagesManager.Instance.AppendTestSkipHistory(
                "Unknown", 4,
                45,
                47,
                48,
                RouteNode(route9, 49, 50, 51),
                52,
                RouteNode(route10, 53, 54, 55));
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

    private static void ApplyCompletedDayContactState(int day, TestRoute fallbackRoute)
    {
        if (MessagesManager.Instance == null) return;

        switch (day)
        {
            case 1:
                TestRoute route1 = GetEffectiveChoiceRoute(1, fallbackRoute);
                SetContactState("Student Group", 75);
                SetContactState("Connect", RouteNode(route1, 1, 2, 3));
                SetContactState("Emma", 8);
                SetContactState("Alex", 13);
                SetContactState("Jamie", 14);
                break;

            case 2:
                TestRoute route3 = GetEffectiveChoiceRoute(3, fallbackRoute);
                TestRoute route5 = GetEffectiveChoiceRoute(5, fallbackRoute);
                SetContactState("Alex", 15);
                SetContactState("Emma", RouteNode(route3, 17, 18, 19));
                SetContactState("Jamie", RouteNode(route5, 25, 26, 27));
                SetContactState("Noah", 28);
                break;

            case 3:
                TestRoute route7 = GetEffectiveChoiceRoute(7, fallbackRoute);
                TestRoute route8 = GetEffectiveChoiceRoute(8, fallbackRoute);
                SetContactState("Emma", 30);
                if (route7 == TestRoute.Safe)
                {
                    SetContactState("Noah", 37);
                }
                else
                {
                    SetContactState("Noah", RouteNode(route8, 41, 42, 43));
                }
                SetContactState("Unknown", 44);
                break;

            case 4:
                TestRoute route10 = GetEffectiveChoiceRoute(10, fallbackRoute);
                SetContactState("Emma", 46);
                SetContactState("Unknown", RouteNode(route10, 53, 54, 55));
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
            int choiceIndex = choice;
            SubscribeChoice(choiceIndex, TestRoute.Safe, $"Choice{choice}_Safe", -1);
            SubscribeChoice(choiceIndex, TestRoute.Neutral, $"Choice{choice}_Neutral", 0);
            SubscribeChoice(choiceIndex, TestRoute.Risky, $"Choice{choice}_Risky", 1);
        }
    }

    private void SubscribeChoice(int choiceIndex, TestRoute route, string eventName, int riskDelta)
    {
        Action action = () =>
        {
            _riskScore += riskDelta;
            _recordedChoices[choiceIndex] = route;
            if (DecisionDataManager.Instance != null)
            {
                DecisionDataManager.Instance.SetOrAddIntVariable($"Scenario_Choice_{choiceIndex}", (int)route);
            }
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
        _lastResolvedEndingNode = endingNode;
        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.SetOrAddIntVariable("Scenario_EndingNode", endingNode);
        }

        Debug.Log($"[DayManager] Resolving campaign ending with score {finalScore}; node {endingNode}.");
        MessagesManager.Instance?.AdvanceContactNarrativeNodeByEvent("Riley", endingNode);
    }

    #endregion
}
