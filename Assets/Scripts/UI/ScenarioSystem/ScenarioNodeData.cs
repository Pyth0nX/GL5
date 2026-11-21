using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks whether a node or transition was taken in the current playthrough,
/// discovered in a previous playthrough within the same save slot, or remains undiscovered.
/// </summary>
public enum ScenarioNodeVisitState
{
    Undiscovered = 0,
    PreviousRun = 1,
    CurrentRun = 2
}

/// <summary>
/// Inspector-configurable override allowing designers to toggle whether a specific NarrativeNode appears
/// in the endgame ScenarioSystem summary screen and optionally customize its title, annotation, sprites, or XY position.
/// </summary>
[Serializable]
public class ScenarioNodeOverride
{
    [Tooltip("ID of the NarrativeNode in narrativeNodes.json.")]
    public int narrativeNodeId;

    [Tooltip("If false, this narrative decision node will be excluded from the ScenarioSystem endgame tree.")]
    public bool showInScenarioTree = true;

    [Tooltip("Optional override for the short title shown inside the node when revealed.")]
    public string customTitle;

    [Tooltip("Optional override for the detail description when this node was discovered.")]
    [TextArea(2, 4)]
    public string customAnnotation;

    [Tooltip("Optional custom sprite for this specific node when taken/revealed.")]
    public Sprite customTakenSprite;

    [Tooltip("Optional custom sprite for this specific node when untaken ('???').")]
    public Sprite customUntakenSprite;

    [Tooltip("Optional representative situation illustration displayed on the left side of the 70% detail screen.")]
    public Sprite detailSituationImage;

    [Tooltip("If enabled, places this node/stage at the specified XY graph coordinate instead of auto-layout.")]
    public bool useCustomGraphPosition;

    [Tooltip("Manual XY coordinates used when useCustomGraphPosition is enabled.")]
    public Vector2 customGraphPosition;
}

/// <summary>
/// Runtime view-model for a single node box inside the ScenarioSystem XY flowchart.
/// </summary>
[Serializable]
public class ScenarioOptionNodeData
{
    public string nodeKey;
    public int parentNodeId;
    public int optionIndex;
    public int resolvedNextNodeId;
    public int storyDay;
    public string stageTitle;
    public string revealedLabel;
    public string revealedAnnotation;
    public ScenarioNodeVisitState visitState;
    public bool isTaken;
    public bool isRootNode;
    public bool isEndingNode;
    public int endingNodeId;
    public Sprite customSprite;
    public Sprite situationImage;
    public bool hasCustomGraphPosition;
    public Vector2 graphPosition;

    public bool IsDiscovered =>
        visitState == ScenarioNodeVisitState.CurrentRun ||
        visitState == ScenarioNodeVisitState.PreviousRun ||
        isTaken;

    /// <summary>
    /// Returns the text to display inside the node box according to whether the player discovered this decision.
    /// Undiscovered nodes always display "???".
    /// </summary>
    public string GetDisplayLabel()
    {
        if (!IsDiscovered)
        {
            return "???";
        }

        return string.IsNullOrWhiteSpace(revealedLabel) ? "Decision\nMade" : revealedLabel;
    }

    /// <summary>
    /// Returns the detailed description shown inside the 70% node inspection screen.
    /// Undiscovered nodes always display "???".
    /// </summary>
    public string GetDisplayAnnotation()
    {
        if (!IsDiscovered)
        {
            return "???";
        }

        return string.IsNullOrWhiteSpace(revealedAnnotation)
            ? "You selected this narrative path during your playthrough."
            : revealedAnnotation;
    }
}

/// <summary>
/// Directed edge between two scenario nodes inside the XY flowchart.
/// </summary>
[Serializable]
public class ScenarioEdgeData
{
    public string fromNodeKey;
    public string toNodeKey;
    public ScenarioNodeVisitState visitState;
    public bool isEndingConnection;
}

/// <summary>
/// Represents a vertical stage column in the ScenarioSystem decision tree flowchart.
/// </summary>
[Serializable]
public class ScenarioColumnData
{
    public int columnIndex;
    public int narrativeNodeId;
    public int choiceNumber;
    public int storyDay;
    public string columnHeader;
    public bool isRootEntryColumn;
    public bool isEndingColumn;
    public bool isSkippedBranchColumn;
    public List<ScenarioOptionNodeData> nodes = new List<ScenarioOptionNodeData>();
}

/// <summary>
/// Complete data model populated by ScenarioDataCollector to render the ScenarioSystem flowchart.
/// </summary>
public class ScenarioTreeModel
{
    public ScenarioColumnData rootColumn;
    public List<ScenarioColumnData> decisionColumns = new List<ScenarioColumnData>();
    public ScenarioColumnData endingColumn;
    public List<ScenarioEdgeData> edges = new List<ScenarioEdgeData>();
    public int finalReputation = 50;
    public int narrativeRiskScore = 0;
    public int reachedEndingNodeId = 72;
    public string reachedEndingTitle = "NEUTRAL ENDING";
    public bool isCampaignCompleted = false;
    public bool hasPreviousRunHistory = false;
    public float discoveryPercent = 0f;
    public int discoveredNodeCount = 0;
    public int totalDiscoverableNodeCount = 0;
}

/// <summary>
/// Reads narrative state non-destructively from DayManager, DecisionDataManager, ReputationManager, and MessagesManager
/// to build the complete ScenarioTreeModel for the endgame summary screen.
/// </summary>
public static class ScenarioDataCollector
{
    private struct CampaignDecisionSpec
    {
        public int nodeId;
        public int choiceNumber;
        public int day;
        public string contactName;
        public string stageTitle;
        public string safeShortLabel;
        public string neutralShortLabel;
        public string riskyShortLabel;
        public string safeAnnotation;
        public string neutralAnnotation;
        public string riskyAnnotation;
        public int safeBranchNode;
        public int neutralBranchNode;
        public int riskyBranchNode;
        public bool dependsOnChoice7NonSafe;
    }

    // Canonical mapping of the 13 campaign decision nodes across the 5 days
    private static readonly CampaignDecisionSpec[] CampaignSpecs = new CampaignDecisionSpec[]
    {
        new CampaignDecisionSpec
        {
            nodeId = 0, choiceNumber = 1, day = 1, contactName = "Connect",
            stageTitle = "Day 1 - Connect Profile",
            safeShortLabel = "Day 1: Profile\nUsername & Avatar",
            neutralShortLabel = "Day 1: Profile\nFirst Name & Selfie",
            riskyShortLabel = "Day 1: Profile\nFull Name & School",
            safeAnnotation = "Day 1 (Connect Setup): You created your Connect profile using only a username and avatar, keeping Luna's personal identity and school private (+6 Safety).",
            neutralAnnotation = "Day 1 (Connect Setup): You created your Connect profile with Luna's first name and a personal selfie, revealing moderate personal information.",
            riskyAnnotation = "Day 1 (Connect Setup): You published Luna's full name, school, and personal photo publicly on Connect, exposing her identity to strangers (-6 Safety).",
            safeBranchNode = 1, neutralBranchNode = 2, riskyBranchNode = 3
        },
        new CampaignDecisionSpec
        {
            nodeId = 9, choiceNumber = 2, day = 1, contactName = "Alex",
            stageTitle = "Day 1 - Chat with Alex",
            safeShortLabel = "Day 1: Alex\nTalk About Books",
            neutralShortLabel = "Day 1: Alex\nShare Hobbies",
            riskyShortLabel = "Day 1: Alex\nShare Personal Details",
            safeAnnotation = "Day 1 (Alex): When Alex asked personal questions, you kept the conversation focused strictly on the book 'Lost Cause' (+6 Safety).",
            neutralAnnotation = "Day 1 (Alex): You shared general hobbies and interests with Alex without giving away sensitive location data.",
            riskyAnnotation = "Day 1 (Alex): You overshared personal details about yourself with someone you just met online (-6 Safety).",
            safeBranchNode = 10, neutralBranchNode = 11, riskyBranchNode = 12
        },
        new CampaignDecisionSpec
        {
            nodeId = 16, choiceNumber = 3, day = 2, contactName = "Emma",
            stageTitle = "Day 2 - Viral Post Reaction",
            safeShortLabel = "Day 2: Emma\nQuestion the Rumor",
            neutralShortLabel = "Day 2: Emma\nSay It's Weird",
            riskyShortLabel = "Day 2: Emma\nLaugh Along",
            safeAnnotation = "Day 2 (Emma): You questioned whether the viral rumor about Sophie was even true, refusing to fuel online humiliation (+6 Safety).",
            neutralAnnotation = "Day 2 (Emma): You stayed passive about the viral post mocking Sophie, calling the situation weird without intervening.",
            riskyAnnotation = "Day 2 (Emma): You laughed along at the viral post targeting Sophie, encouraging online pile-ons (-6 Safety).",
            safeBranchNode = 17, neutralBranchNode = 18, riskyBranchNode = 19
        },
        new CampaignDecisionSpec
        {
            nodeId = 20, choiceNumber = 4, day = 2, contactName = "Jamie",
            stageTitle = "Day 2 - Jamie's Post",
            safeShortLabel = "Day 2: Jamie\nWarn About Harm",
            neutralShortLabel = "Day 2: Jamie\nStay Out of It",
            riskyShortLabel = "Day 2: Jamie\nSupport the Joke",
            safeAnnotation = "Day 2 (Jamie): You told Jamie that reposting humiliating content could seriously hurt someone (+6 Safety).",
            neutralAnnotation = "Day 2 (Jamie): You chose to stay out of Jamie's post rather than challenging or supporting it.",
            riskyAnnotation = "Day 2 (Jamie): You validated Jamie's mocking post as just a joke, normalizing cyberbullying (-6 Safety).",
            safeBranchNode = 21, neutralBranchNode = 22, riskyBranchNode = 23
        },
        new CampaignDecisionSpec
        {
            nodeId = 24, choiceNumber = 5, day = 2, contactName = "Jamie",
            stageTitle = "Day 2 - Public Response",
            safeShortLabel = "Day 2: Connect\nReport & Refuse",
            neutralShortLabel = "Day 2: Connect\nScroll Past Silently",
            riskyShortLabel = "Day 2: Connect\nLike & Repost",
            safeAnnotation = "Day 2 (Public Post): You refused to amplify the humiliating post and stood against spreading it further (+6 Safety).",
            neutralAnnotation = "Day 2 (Public Post): You scrolled past the viral post without interacting or reporting it.",
            riskyAnnotation = "Day 2 (Public Post): You actively liked and reposted the viral post, increasing its reach (-6 Safety).",
            safeBranchNode = 25, neutralBranchNode = 26, riskyBranchNode = 27
        },
        new CampaignDecisionSpec
        {
            nodeId = 32, choiceNumber = 6, day = 3, contactName = "Noah",
            stageTitle = "Day 3 - Noah's Age Question",
            safeShortLabel = "Day 3: Noah\nKeep Age Private",
            neutralShortLabel = "Day 3: Noah\nGive Vague Answer",
            riskyShortLabel = "Day 3: Noah\nReveal Exact Age",
            safeAnnotation = "Day 3 (Noah): You refused to disclose your age to Noah, maintaining strong personal boundaries online (+6 Safety).",
            neutralAnnotation = "Day 3 (Noah): You gave Noah a vague answer about your age and school grade.",
            riskyAnnotation = "Day 3 (Noah): You revealed your exact age and personal details to Noah (-6 Safety).",
            safeBranchNode = 33, neutralBranchNode = 34, riskyBranchNode = 35
        },
        new CampaignDecisionSpec
        {
            nodeId = 36, choiceNumber = 7, day = 3, contactName = "Noah",
            stageTitle = "Day 3 - Noah's Photo Request",
            safeShortLabel = "Day 3: Photo\nRefuse to Send",
            neutralShortLabel = "Day 3: Photo\nSend Casual Photo",
            riskyShortLabel = "Day 3: Photo\nSend Private Photo",
            safeAnnotation = "Day 3 (Noah Photo Request): You firmly refused to send a photo to Noah. Because you protected your image, Noah had nothing to leak, completely avoiding the leaked-photo crisis (+6 Safety).",
            neutralAnnotation = "Day 3 (Noah Photo Request): You hesitated or sent a casual picture, which Noah still screenshotted and reposted without your consent.",
            riskyAnnotation = "Day 3 (Noah Photo Request): You trusted Noah and sent a personal photo, which was immediately leaked and reposted publicly (-6 Safety).",
            safeBranchNode = 37, neutralBranchNode = 38, riskyBranchNode = 39
        },
        new CampaignDecisionSpec
        {
            nodeId = 40, choiceNumber = 8, day = 3, contactName = "Noah",
            stageTitle = "Day 3 - Leaked Photo Crisis",
            safeShortLabel = "Day 3: Leak\nDemand Removal",
            neutralShortLabel = "Day 3: Leak\nAsk Why He Posted",
            riskyShortLabel = "Day 3: Leak\nRetaliate in Anger",
            safeAnnotation = "Day 3 (Leaked Photo): After Noah reposted your photo without permission, you demanded its removal and documented the abuse (+6 Safety).",
            neutralAnnotation = "Day 3 (Leaked Photo): You pleaded with Noah asking why he reposted your photo publicly.",
            riskyAnnotation = "Day 3 (Leaked Photo): You lashed out publicly in the comments, escalating the conflict around the leaked photo (-6 Safety).",
            safeBranchNode = 41, neutralBranchNode = 42, riskyBranchNode = 43,
            dependsOnChoice7NonSafe = true
        },
        new CampaignDecisionSpec
        {
            nodeId = 48, choiceNumber = 9, day = 4, contactName = "Unknown",
            stageTitle = "Day 4 - Stalker Message",
            safeShortLabel = "Day 4: Unknown\nRefuse & Block",
            neutralShortLabel = "Day 4: Unknown\nAsk Who They Are",
            riskyShortLabel = "Day 4: Unknown\nConfirm Routine",
            safeAnnotation = "Day 4 (Unknown Contact): When an anonymous account claimed to know your route home, you refused to engage and protected your location (+6 Safety).",
            neutralAnnotation = "Day 4 (Unknown Contact): You replied to the anonymous stalker asking who they were, keeping the channel open.",
            riskyAnnotation = "Day 4 (Unknown Contact): You confirmed details about your real-world schedule and route to an anonymous stranger (-6 Safety).",
            safeBranchNode = 49, neutralBranchNode = 50, riskyBranchNode = 51
        },
        new CampaignDecisionSpec
        {
            nodeId = 52, choiceNumber = 10, day = 4, contactName = "Unknown",
            stageTitle = "Day 4 - Account Privacy",
            safeShortLabel = "Day 4: Security\nLock Down Profile",
            neutralShortLabel = "Day 4: Security\nPartial Privacy",
            riskyShortLabel = "Day 4: Security\nKeep Profile Public",
            safeAnnotation = "Day 4 (Account Security): You locked down your privacy settings and restricted strangers from tracking your profile (+6 Safety).",
            neutralAnnotation = "Day 4 (Account Security): You made minor adjustments but left parts of your profile accessible.",
            riskyAnnotation = "Day 4 (Account Security): You ignored the warning signs and kept your profile and location tags public (-6 Safety).",
            safeBranchNode = 53, neutralBranchNode = 54, riskyBranchNode = 55
        },
        new CampaignDecisionSpec
        {
            nodeId = 59, choiceNumber = 11, day = 5, contactName = "Riley",
            stageTitle = "Day 5 - Impersonation Rumor",
            safeShortLabel = "Day 5: Riley\nDenounce Fake Post",
            neutralShortLabel = "Day 5: Riley\nExplain Quietly",
            riskyShortLabel = "Day 5: Riley\nAttack Accusers",
            safeAnnotation = "Day 5 (Riley): When a fake edited screenshot circulated in your name, you calmly denounced the fabrication and gathered evidence (+6 Safety).",
            neutralAnnotation = "Day 5 (Riley): You tried to explain yourself hesitantly without taking decisive action against the impersonator.",
            riskyAnnotation = "Day 5 (Riley): You reacted with hostility online, fueling the impersonator's narrative (-6 Safety).",
            safeBranchNode = 60, neutralBranchNode = 61, riskyBranchNode = 62
        },
        new CampaignDecisionSpec
        {
            nodeId = 63, choiceNumber = 12, day = 5, contactName = "Riley",
            stageTitle = "Day 5 - Verification Trap",
            safeShortLabel = "Day 5: Riley\nNever Share Login",
            neutralShortLabel = "Day 5: Riley\nHesitate on Link",
            riskyShortLabel = "Day 5: Riley\nSend Credentials",
            safeAnnotation = "Day 5 (Phishing Attempt): You recognized Riley's request for account details as a trap and refused to share your credentials (+6 Safety).",
            neutralAnnotation = "Day 5 (Phishing Attempt): You questioned the request but shared partial verification info.",
            riskyAnnotation = "Day 5 (Phishing Attempt): You handed over sensitive account access details under pressure (-6 Safety).",
            safeBranchNode = 64, neutralBranchNode = 65, riskyBranchNode = 66
        },
        new CampaignDecisionSpec
        {
            nodeId = 67, choiceNumber = 13, day = 5, contactName = "Riley",
            stageTitle = "Day 5 - Final Resolution",
            safeShortLabel = "Day 5: Final\nReport & Seek Help",
            neutralShortLabel = "Day 5: Final\nDelete App Alone",
            riskyShortLabel = "Day 5: Final\nConfront Alone",
            safeAnnotation = "Day 5 (Final Decision): You saved the evidence, reported the harassment, and reached out to trusted adults and friends for support (+6 Safety).",
            neutralAnnotation = "Day 5 (Final Decision): You tried to move on quietly without reporting the full harassment chain.",
            riskyAnnotation = "Day 5 (Final Decision): You tried to handle the escalating threats entirely on your own without support (-6 Safety).",
            safeBranchNode = 68, neutralBranchNode = 69, riskyBranchNode = 70
        }
    };

    public const int RootNodeId = -100;
    public const int ConnectIntroSkipNodeId = 4;
    public const int GoodEndingNodeId = 71;
    public const int NeutralEndingNodeId = 72;
    public const int BadEndingNodeId = 73;

    private struct EndingSpec
    {
        public int id;
        public int optionIndex;
        public string defaultTitle;
        public string headline;
        public string defaultLabel;
        public string defaultAnnotation;
    }

    private static readonly EndingSpec[] CanonicalEndings =
    {
        new EndingSpec
        {
            id = GoodEndingNodeId,
            optionIndex = 0,
            defaultTitle = "Good Ending",
            headline = "GOOD ENDING",
            defaultLabel = "GOOD ENDING\nSafe & Supported",
            defaultAnnotation = "GOOD ENDING: Luna protected her boundaries, documented the harassment, and trusted the people around her. The impersonation account was removed and she regained her peace of mind."
        },
        new EndingSpec
        {
            id = NeutralEndingNodeId,
            optionIndex = 1,
            defaultTitle = "Neutral Ending",
            headline = "NEUTRAL ENDING",
            defaultLabel = "NEUTRAL ENDING\nLingering Doubt",
            defaultAnnotation = "NEUTRAL ENDING: The worst rumors eventually faded, but the leaked posts and unanswered messages left Luna cautious and uneasy every time her phone buzzed."
        },
        new EndingSpec
        {
            id = BadEndingNodeId,
            optionIndex = 2,
            defaultTitle = "Bad Ending",
            headline = "BAD ENDING",
            defaultLabel = "BAD ENDING\nExposed & Overwhelmed",
            defaultAnnotation = "BAD ENDING: Oversharing personal information, photos, and credentials snowballed into severe harassment and impersonation that followed Luna into school."
        }
    };

    public const string RootNodeKey = "ROOT";

    public static string MakeDecisionNodeKey(int parentNodeId, int optionIndex) => $"N{parentNodeId}_O{optionIndex}";
    public static string MakeEndingNodeKey(int endingNodeId) => $"E{endingNodeId}";
    public static string MakeEdgeKey(string fromKey, string toKey) => $"{fromKey}->{toKey}";

    /// <summary>
    /// Returns true if the specified NarrativeNode ID is one of the canonical campaign ending nodes.
    /// </summary>
    public static bool IsCanonicalEndingNodeId(int nodeId)
    {
        for (int i = 0; i < CanonicalEndings.Length; i++)
        {
            if (CanonicalEndings[i].id == nodeId)
            {
                return true;
            }
        }
        return false;
    }

    private static string GetEndingHeadline(int endingNodeId)
    {
        for (int i = 0; i < CanonicalEndings.Length; i++)
        {
            if (CanonicalEndings[i].id == endingNodeId)
            {
                return CanonicalEndings[i].headline;
            }
        }
        return "Campaign in Progress";
    }

    private static HashSet<int> BuildCanonicalExcludedNodeIds()
    {
        HashSet<int> ids = new HashSet<int> { ConnectIntroSkipNodeId };
        for (int i = 0; i < CanonicalEndings.Length; i++)
        {
            ids.Add(CanonicalEndings[i].id);
        }
        for (int i = 0; i < CampaignSpecs.Length; i++)
        {
            ids.Add(CampaignSpecs[i].nodeId);
        }
        return ids;
    }

    /// <summary>
    /// Returns the ordered canonical (nodeId, stageTitle) list shared with ScenarioSituationImageCatalog.
    /// </summary>
    public static List<(int nodeId, string title)> GetDefaultSituationCatalogEntries()
    {
        var entries = new List<(int nodeId, string title)>(1 + CampaignSpecs.Length + CanonicalEndings.Length)
        {
            (RootNodeId, "Campaign Start - Connect Installed")
        };

        for (int i = 0; i < CampaignSpecs.Length; i++)
        {
            entries.Add((CampaignSpecs[i].nodeId, CampaignSpecs[i].stageTitle));
        }

        for (int i = 0; i < CanonicalEndings.Length; i++)
        {
            entries.Add((CanonicalEndings[i].id, CanonicalEndings[i].defaultTitle));
        }

        return entries;
    }

    /// <summary>
    /// Counts the total discoverable narrative option nodes + canonical endings across the campaign.
    /// Automatically scales when designers add extra decision nodes or custom options in narrativeNodes.json.
    /// </summary>
    public static int GetTotalDiscoverableNodeCount(IReadOnlyDictionary<int, NarrativeNode> allNodes = null)
    {
        if (allNodes == null && MessagesManager.Instance != null)
        {
            allNodes = MessagesManager.Instance.GetAllNarrativeNodes();
        }

        int total = 0;
        HashSet<int> counted = new HashSet<int> { ConnectIntroSkipNodeId };
        for (int i = 0; i < CanonicalEndings.Length; i++)
        {
            counted.Add(CanonicalEndings[i].id);
        }

        for (int i = 0; i < CampaignSpecs.Length; i++)
        {
            CampaignDecisionSpec spec = CampaignSpecs[i];
            counted.Add(spec.nodeId);

            if (allNodes != null && allNodes.TryGetValue(spec.nodeId, out NarrativeNode node) && node != null)
            {
                if (!node.ShowInScenarioTree()) continue;

                Option[] opts = node.Options();
                if (opts != null && opts.Length > 0)
                {
                    for (int o = 0; o < opts.Length; o++)
                    {
                        if (opts[o] != null && opts[o].ShowInScenarioTree())
                        {
                            total++;
                        }
                    }
                    continue;
                }
            }

            total += 3;
        }

        if (allNodes != null)
        {
            foreach (KeyValuePair<int, NarrativeNode> kvp in allNodes)
            {
                NarrativeNode node = kvp.Value;
                if (node == null || counted.Contains(node.ID()) || !node.HasOptions() || !node.ShowInScenarioTree())
                {
                    continue;
                }

                Option[] opts = node.Options();
                if (opts == null) continue;

                for (int o = 0; o < opts.Length; o++)
                {
                    if (opts[o] != null && opts[o].ShowInScenarioTree())
                    {
                        total++;
                    }
                }
            }
        }

        total += CanonicalEndings.Length;
        return Mathf.Max(1, total);
    }

    /// <summary>
    /// Computes the percentage (0..100) of scenario decision & ending nodes discovered from canonical node keys.
    /// </summary>
    public static float ComputeDiscoveryPercentage(
        IEnumerable<string> discoveredKeys,
        IReadOnlyDictionary<int, NarrativeNode> allNodes = null)
    {
        if (discoveredKeys == null)
        {
            return 0f;
        }

        HashSet<string> uniqueDiscoverables = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in discoveredKeys)
        {
            if (IsDiscoverableKey(key))
            {
                uniqueDiscoverables.Add(key);
            }
        }

        int total = GetTotalDiscoverableNodeCount(allNodes);
        return Mathf.Clamp((uniqueDiscoverables.Count / (float)total) * 100f, 0f, 100f);
    }

    /// <summary>
    /// Fallback calculation for legacy save files that predate discoveredScenarioNodes.
    /// </summary>
    public static float ComputeLegacyDiscoveryPercentage(List<DecisionVariableSaveData> decisionVariables)
    {
        if (decisionVariables == null || decisionVariables.Count == 0)
        {
            return 0f;
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        ExtractKeysFromDecisionVariables(decisionVariables, keys);
        return ComputeDiscoveryPercentage(keys);
    }

    public static void ExtractKeysFromDecisionVariables(
        IReadOnlyList<DecisionVariableSaveData> decisionVariables,
        HashSet<string> destinationKeys)
    {
        if (decisionVariables == null || destinationKeys == null)
        {
            return;
        }

        for (int i = 0; i < decisionVariables.Count; i++)
        {
            DecisionVariableSaveData variable = decisionVariables[i];
            if (string.IsNullOrEmpty(variable.name) || !int.TryParse(variable.serializedValue, out int intVal))
            {
                continue;
            }

            if (variable.name.StartsWith("Scenario_Choice_", StringComparison.Ordinal) &&
                int.TryParse(variable.name.Substring("Scenario_Choice_".Length), out int choiceNumber))
            {
                for (int s = 0; s < CampaignSpecs.Length; s++)
                {
                    if (CampaignSpecs[s].choiceNumber == choiceNumber)
                    {
                        int optIdx = intVal switch
                        {
                            (int)DayManager.TestRoute.Safe => 0,
                            (int)DayManager.TestRoute.Neutral => 1,
                            (int)DayManager.TestRoute.Risky => 2,
                            _ => 1
                        };
                        destinationKeys.Add(MakeDecisionNodeKey(CampaignSpecs[s].nodeId, optIdx));
                        break;
                    }
                }
            }
            else if (variable.name.StartsWith("Scenario_Node_", StringComparison.Ordinal) &&
                     int.TryParse(variable.name.Substring("Scenario_Node_".Length), out int nodeId) &&
                     intVal >= 0)
            {
                destinationKeys.Add(MakeDecisionNodeKey(nodeId, intVal));
            }
            else if (variable.name == "Scenario_EndingNode" &&
                     IsCanonicalEndingNodeId(intVal))
            {
                destinationKeys.Add(MakeEndingNodeKey(intVal));
            }
        }
    }

    private static bool IsDiscoverableKey(string key)
    {
        return !string.IsNullOrEmpty(key) && (key[0] == 'N' || key[0] == 'E');
    }

    /// <summary>
    /// Collects all canonical node keys and consecutive transition edge keys from the active playthrough.
    /// </summary>
    public static void CollectCurrentRunKeys(out HashSet<string> nodeKeys, out HashSet<string> edgeKeys)
    {
        nodeKeys = new HashSet<string>(StringComparer.Ordinal);
        edgeKeys = new HashSet<string>(StringComparer.Ordinal);

        IReadOnlyDictionary<int, NarrativeNode> allNodes = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetAllNarrativeNodes()
            : null;

        List<string> orderedTakenKeys = new List<string>();
        if (IsConnectAppDownloaded())
        {
            nodeKeys.Add(RootNodeKey);
            orderedTakenKeys.Add(RootNodeKey);
        }

        for (int i = 0; i < CampaignSpecs.Length; i++)
        {
            CampaignDecisionSpec spec = CampaignSpecs[i];
            int selectedOpt = ResolveSelectedOptionIndex(spec, allNodes);
            if (selectedOpt >= 0)
            {
                string key = MakeDecisionNodeKey(spec.nodeId, selectedOpt);
                nodeKeys.Add(key);
                orderedTakenKeys.Add(key);
            }
        }

        if (allNodes != null)
        {
            HashSet<int> canonicalIds = BuildCanonicalExcludedNodeIds();

            List<int> extraIds = new List<int>();
            foreach (KeyValuePair<int, NarrativeNode> kvp in allNodes)
            {
                NarrativeNode n = kvp.Value;
                if (n == null || canonicalIds.Contains(n.ID())) continue;
                if (n.HasOptions() && n.Options() != null && n.Options().Length > 0 && n.ShowInScenarioTree())
                {
                    extraIds.Add(n.ID());
                }
            }
            extraIds.Sort();

            for (int i = 0; i < extraIds.Count; i++)
            {
                int selectedExtra = ResolveCustomNodeSelectedOption(extraIds[i]);
                if (selectedExtra >= 0)
                {
                    string key = MakeDecisionNodeKey(extraIds[i], selectedExtra);
                    nodeKeys.Add(key);
                    orderedTakenKeys.Add(key);
                }
            }
        }

        if (IsCampaignCompleted())
        {
            int endingId = ResolveReachedEndingNodeId(allNodes);
            string endingKey = MakeEndingNodeKey(endingId);
            nodeKeys.Add(endingKey);
            orderedTakenKeys.Add(endingKey);
        }

        for (int i = 0; i + 1 < orderedTakenKeys.Count; i++)
        {
            edgeKeys.Add(MakeEdgeKey(orderedTakenKeys[i], orderedTakenKeys[i + 1]));
        }
    }

    /// <summary>
    /// Builds the complete ScenarioTreeModel by inspecting the current playthrough and merging it with
    /// previous playthrough discoveries stored in SaveManager so taken/previous/undiscovered paths are distinct.
    /// </summary>
    public static ScenarioTreeModel CollectTreeModel(
        List<ScenarioNodeOverride> inspectorOverrides = null,
        IReadOnlyCollection<string> previousDiscoveredNodes = null,
        IReadOnlyCollection<string> previousDiscoveredEdges = null)
    {
        ScenarioTreeModel model = new ScenarioTreeModel();

        if (previousDiscoveredNodes == null && SaveManager.HasInstance)
        {
            previousDiscoveredNodes = SaveManager.Instance.DiscoveredScenarioNodes;
        }
        if (previousDiscoveredEdges == null && SaveManager.HasInstance)
        {
            previousDiscoveredEdges = SaveManager.Instance.DiscoveredScenarioEdges;
        }

        HashSet<string> prevNodeSet = previousDiscoveredNodes != null
            ? new HashSet<string>(previousDiscoveredNodes, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> prevEdgeSet = previousDiscoveredEdges != null
            ? new HashSet<string>(previousDiscoveredEdges, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        // 1. Read final Reputation and Risk Score
        if (ReputationManager.Instance != null)
        {
            model.finalReputation = ReputationManager.Instance.CurrentReputation;
        }

        if (DayManager.Instance != null)
        {
            model.narrativeRiskScore = DayManager.Instance.NarrativeRiskScore;
        }

        // Build lookup dictionary for Inspector overrides
        Dictionary<int, ScenarioNodeOverride> overrideMap = new Dictionary<int, ScenarioNodeOverride>();
        if (inspectorOverrides != null)
        {
            for (int i = 0; i < inspectorOverrides.Count; i++)
            {
                ScenarioNodeOverride ov = inspectorOverrides[i];
                if (ov != null)
                {
                    overrideMap[ov.narrativeNodeId] = ov;
                }
            }
        }

        IReadOnlyDictionary<int, NarrativeNode> allNodes = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetAllNarrativeNodes()
            : null;

        bool isCampaignCompleted = IsCampaignCompleted();
        model.isCampaignCompleted = isCampaignCompleted;

        bool isConnectDownloaded = IsConnectAppDownloaded();
        bool rootWasInPreviousRun = prevNodeSet.Contains(RootNodeKey);

        // 2. Root node ("Luna joins Connect") appears when Connect was downloaded in this or an earlier run
        if (isConnectDownloaded || rootWasInPreviousRun)
        {
            ScenarioNodeVisitState rootVisit = isConnectDownloaded
                ? ScenarioNodeVisitState.CurrentRun
                : ScenarioNodeVisitState.PreviousRun;

            if (rootVisit == ScenarioNodeVisitState.PreviousRun)
            {
                model.hasPreviousRunHistory = true;
            }

            model.rootColumn = new ScenarioColumnData
            {
                columnIndex = 0,
                narrativeNodeId = RootNodeId,
                choiceNumber = 0,
                storyDay = 1,
                columnHeader = "Start",
                isRootEntryColumn = true,
                isEndingColumn = false
            };

            string rootTitle = "Day 1\nConnect Installed";
            string rootAnnotation = "Start of 'Before You Send': Luna joins the Connect social network at her new school. Every decision shapes her digital footprint and safety across the 5 days.";

            if (overrideMap.TryGetValue(RootNodeId, out ScenarioNodeOverride rootOverride) && rootOverride != null)
            {
                if (!string.IsNullOrWhiteSpace(rootOverride.customTitle)) rootTitle = rootOverride.customTitle;
                if (!string.IsNullOrWhiteSpace(rootOverride.customAnnotation)) rootAnnotation = rootOverride.customAnnotation;
            }

            ResolvedNodeVisuals rootVisuals = ResolveNodeVisualOverrides(RootNodeId, rootVisit, overrideMap);

            model.rootColumn.nodes.Add(new ScenarioOptionNodeData
            {
                nodeKey = RootNodeKey,
                parentNodeId = RootNodeId,
                optionIndex = 0,
                resolvedNextNodeId = 0,
                storyDay = 1,
                stageTitle = "Campaign Start",
                revealedLabel = rootTitle,
                revealedAnnotation = rootAnnotation,
                visitState = rootVisit,
                isTaken = rootVisit == ScenarioNodeVisitState.CurrentRun,
                isRootNode = true,
                isEndingNode = false,
                customSprite = rootVisuals.customSprite,
                situationImage = rootVisuals.situationImage,
                hasCustomGraphPosition = rootVisuals.useCustomGraphPosition,
                graphPosition = rootVisuals.customGraphPosition
            });
        }
        else
        {
            model.rootColumn = null;
        }

        // 3. Determine whether Choice 7 (Node 36) was Safe, which skips Choice 8 (Node 40)
        int choice7SelectedIndex = ResolveSelectedOptionIndex(CampaignSpecs[6], allNodes);
        bool choice7WasTaken = (choice7SelectedIndex >= 0);
        bool choice7WasSafe = choice7WasTaken && (choice7SelectedIndex == 0);
        bool choice8PreviouslyDiscovered = HasAnyOptionInSet(CampaignSpecs[7].nodeId, 3, prevNodeSet);

        HashSet<int> handledNodeIds = new HashSet<int>();
        int nextColumnIndex = model.rootColumn != null ? 1 : 0;

        // 4. Populate canonical campaign decision columns (Nodes 0..67)
        for (int i = 0; i < CampaignSpecs.Length; i++)
        {
            CampaignDecisionSpec spec = CampaignSpecs[i];
            handledNodeIds.Add(spec.nodeId);

            if (!ShouldShowNodeInScenarioTree(spec.nodeId, allNodes, overrideMap))
            {
                continue;
            }

            // If Choice 7 was Safe in the current run and Choice 8 was never discovered in previous runs,
            // Choice 8 belongs exclusively to untaken Neutral/Risky branches and stays hidden.
            // If Choice 8 WAS discovered in a previous run, keep only its discovered option(s) visible.
            if (spec.dependsOnChoice7NonSafe &&
                choice7WasSafe &&
                !choice8PreviouslyDiscovered)
            {
                continue;
            }

            int selectedOptionIndex = (spec.dependsOnChoice7NonSafe && choice7WasSafe)
                ? -1
                : ResolveSelectedOptionIndex(spec, allNodes);

            NarrativeNode narrativeNode = null;
            if (allNodes != null)
            {
                allNodes.TryGetValue(spec.nodeId, out narrativeNode);
            }

            Option[] rawOptions = narrativeNode != null ? narrativeNode.Options() : null;
            int optionCount = (rawOptions != null && rawOptions.Length > 0) ? rawOptions.Length : 3;
            bool colHasPrevDiscovery = HasAnyOptionInSet(spec.nodeId, optionCount, prevNodeSet);

            // In-game preview (before completion): display decisions taken so far or discovered in earlier playthroughs
            if (selectedOptionIndex < 0 && !colHasPrevDiscovery)
            {
                continue;
            }

            ScenarioOverridePair nodeLevelOverride = GetNodeLevelOverride(spec.nodeId, narrativeNode, overrideMap);

            ScenarioColumnData column = new ScenarioColumnData
            {
                columnIndex = nextColumnIndex++,
                narrativeNodeId = spec.nodeId,
                choiceNumber = spec.choiceNumber,
                storyDay = spec.day,
                columnHeader = !string.IsNullOrWhiteSpace(nodeLevelOverride.title) ? nodeLevelOverride.title : spec.stageTitle,
                isRootEntryColumn = false,
                isEndingColumn = false,
                isSkippedBranchColumn = false
            };

            for (int optIdx = 0; optIdx < optionCount; optIdx++)
            {
                Option rawOpt = (rawOptions != null && optIdx < rawOptions.Length) ? rawOptions[optIdx] : null;
                if (rawOpt != null && !rawOpt.ShowInScenarioTree())
                {
                    continue;
                }

                string optKey = MakeDecisionNodeKey(spec.nodeId, optIdx);
                bool isCurrentRunOption = (selectedOptionIndex == optIdx);
                bool isPreviousRunOption = !isCurrentRunOption && prevNodeSet.Contains(optKey);

                ScenarioNodeVisitState visitState = isCurrentRunOption
                    ? ScenarioNodeVisitState.CurrentRun
                    : (isPreviousRunOption ? ScenarioNodeVisitState.PreviousRun : ScenarioNodeVisitState.Undiscovered);

                if (visitState == ScenarioNodeVisitState.PreviousRun)
                {
                    model.hasPreviousRunHistory = true;
                }

                // If Choice 7 was Safe in this run, Choice 8 was skipped; only show options previously discovered
                if (spec.dependsOnChoice7NonSafe &&
                    choice7WasSafe &&
                    visitState == ScenarioNodeVisitState.Undiscovered)
                {
                    continue;
                }

                string defaultLabel = GetSpecOptionLabel(spec, optIdx, rawOpt, optionCount);
                string defaultAnnotation = GetSpecOptionAnnotation(spec, optIdx, rawOpt, nodeLevelOverride.annotation, optionCount);
                int nextNodeId = rawOpt != null ? rawOpt.nextNode : GetSpecBranchNode(spec, optIdx);

                ResolvedNodeVisuals specVisuals = ResolveNodeVisualOverrides(
                    spec.nodeId, visitState, overrideMap, rawOpt, narrativeNode);

                column.nodes.Add(new ScenarioOptionNodeData
                {
                    nodeKey = optKey,
                    parentNodeId = spec.nodeId,
                    optionIndex = optIdx,
                    resolvedNextNodeId = nextNodeId,
                    storyDay = spec.day,
                    stageTitle = column.columnHeader,
                    revealedLabel = defaultLabel,
                    revealedAnnotation = defaultAnnotation,
                    visitState = visitState,
                    isTaken = isCurrentRunOption,
                    isRootNode = false,
                    isEndingNode = false,
                    customSprite = specVisuals.customSprite,
                    situationImage = specVisuals.situationImage,
                    hasCustomGraphPosition = specVisuals.useCustomGraphPosition,
                    graphPosition = specVisuals.customGraphPosition
                });
            }

            if (column.nodes.Count > 0)
            {
                model.decisionColumns.Add(column);
            }
        }

        // 5. Extra designer-defined decision nodes (_hasOptions == true and _showInScenarioTree == true)
        handledNodeIds.Add(ConnectIntroSkipNodeId);
        for (int e = 0; e < CanonicalEndings.Length; e++)
        {
            handledNodeIds.Add(CanonicalEndings[e].id);
        }

        if (allNodes != null)
        {
            List<int> extraNodeIds = new List<int>();
            foreach (KeyValuePair<int, NarrativeNode> kvp in allNodes)
            {
                NarrativeNode node = kvp.Value;
                if (node == null || handledNodeIds.Contains(node.ID()))
                {
                    continue;
                }

                if (node.HasOptions() && node.Options() != null && node.Options().Length > 0)
                {
                    if (ShouldShowNodeInScenarioTree(node.ID(), allNodes, overrideMap))
                    {
                        extraNodeIds.Add(node.ID());
                    }
                }
            }

            extraNodeIds.Sort();
            for (int i = 0; i < extraNodeIds.Count; i++)
            {
                int extraNodeId = extraNodeIds[i];
                NarrativeNode extraNode = allNodes[extraNodeId];
                Option[] opts = extraNode.Options();
                int selectedOpt = ResolveCustomNodeSelectedOption(extraNodeId);
                bool hasPrevExtra = HasAnyOptionInSet(extraNodeId, opts != null ? opts.Length : 0, prevNodeSet);

                if (selectedOpt < 0 && !hasPrevExtra)
                {
                    continue;
                }

                ScenarioOverridePair extraOverride = GetNodeLevelOverride(extraNodeId, extraNode, overrideMap);
                string stageHeader = !string.IsNullOrWhiteSpace(extraOverride.title)
                    ? extraOverride.title
                    : $"Decision #{extraNodeId}";

                ScenarioColumnData extraCol = new ScenarioColumnData
                {
                    columnIndex = nextColumnIndex++,
                    narrativeNodeId = extraNodeId,
                    choiceNumber = model.decisionColumns.Count + 1,
                    storyDay = EstimateStoryDay(extraNode),
                    columnHeader = stageHeader
                };

                for (int optIdx = 0; optIdx < opts.Length; optIdx++)
                {
                    Option opt = opts[optIdx];
                    if (opt == null || !opt.ShowInScenarioTree()) continue;

                    string optKey = MakeDecisionNodeKey(extraNodeId, optIdx);
                    bool currentTaken = (selectedOpt == optIdx);
                    bool prevTaken = !currentTaken && prevNodeSet.Contains(optKey);

                    ScenarioNodeVisitState visitState = currentTaken
                        ? ScenarioNodeVisitState.CurrentRun
                        : (prevTaken ? ScenarioNodeVisitState.PreviousRun : ScenarioNodeVisitState.Undiscovered);

                    if (visitState == ScenarioNodeVisitState.PreviousRun)
                    {
                        model.hasPreviousRunHistory = true;
                    }

                    string fallbackLabel = extraNode.ResponseMode() == PhoneResponseMode.FreeText
                        ? GetDefaultFreeTextRouteLabel(optIdx)
                        : "Decision\nMade";
                    string label = !string.IsNullOrWhiteSpace(opt.GetScenarioLabel())
                        ? opt.GetScenarioLabel()
                        : (!string.IsNullOrWhiteSpace(opt.text) ? FormatShortOptionText(opt.text) : fallbackLabel);
                    string annotation = !string.IsNullOrWhiteSpace(opt.GetScenarioAnnotation())
                        ? opt.GetScenarioAnnotation()
                        : (!string.IsNullOrWhiteSpace(extraOverride.annotation)
                            ? extraOverride.annotation
                            : (!string.IsNullOrWhiteSpace(opt.text) ? opt.text : fallbackLabel));

                    ResolvedNodeVisuals extraVisuals = ResolveNodeVisualOverrides(
                        extraNodeId, visitState, overrideMap, opt, extraNode);

                    extraCol.nodes.Add(new ScenarioOptionNodeData
                    {
                        nodeKey = optKey,
                        parentNodeId = extraNodeId,
                        optionIndex = optIdx,
                        resolvedNextNodeId = opt.nextNode,
                        storyDay = extraCol.storyDay,
                        stageTitle = stageHeader,
                        revealedLabel = label,
                        revealedAnnotation = annotation,
                        visitState = visitState,
                        isTaken = currentTaken,
                        isRootNode = false,
                        isEndingNode = false,
                        customSprite = extraVisuals.customSprite,
                        situationImage = extraVisuals.situationImage,
                        hasCustomGraphPosition = extraVisuals.useCustomGraphPosition,
                        graphPosition = extraVisuals.customGraphPosition
                    });
                }

                if (extraCol.nodes.Count > 0)
                {
                    int insertIdx = model.decisionColumns.Count;
                    for (int c = 0; c < model.decisionColumns.Count; c++)
                    {
                        if (model.decisionColumns[c].storyDay > extraCol.storyDay)
                        {
                            insertIdx = c;
                            break;
                        }
                    }
                    model.decisionColumns.Insert(insertIdx, extraCol);
                }
            }
        }

        int seqColIndex = model.rootColumn != null ? 1 : 0;
        for (int c = 0; c < model.decisionColumns.Count; c++)
        {
            model.decisionColumns[c].columnIndex = seqColIndex++;
        }
        nextColumnIndex = seqColIndex;

        // 6. Special Ending Section: ONLY display endings that have been reached (CurrentRun or PreviousRun).
        // Never display unreached endings or reveal how many total endings exist!
        int reachedEndingId = isCampaignCompleted ? ResolveReachedEndingNodeId(allNodes) : -1;
        model.reachedEndingNodeId = reachedEndingId;
        model.reachedEndingTitle = GetEndingHeadline(reachedEndingId);

        ScenarioColumnData endingColumn = new ScenarioColumnData
        {
            columnIndex = nextColumnIndex,
            narrativeNodeId = reachedEndingId,
            choiceNumber = 14,
            storyDay = 5,
            columnHeader = "ENDING",
            isRootEntryColumn = false,
            isEndingColumn = true
        };

        for (int e = 0; e < CanonicalEndings.Length; e++)
        {
            EndingSpec spec = CanonicalEndings[e];
            string endingKey = MakeEndingNodeKey(spec.id);
            bool isCurrentEnding = isCampaignCompleted && (reachedEndingId == spec.id);
            bool isPreviousEnding = !isCurrentEnding && prevNodeSet.Contains(endingKey);

            // Never expose unreached endings or the total ending count
            if (!isCurrentEnding && !isPreviousEnding)
            {
                continue;
            }

            ScenarioNodeVisitState visitState = isCurrentEnding
                ? ScenarioNodeVisitState.CurrentRun
                : ScenarioNodeVisitState.PreviousRun;

            if (visitState == ScenarioNodeVisitState.PreviousRun)
            {
                model.hasPreviousRunHistory = true;
            }

            AddDiscoveredEndingNode(endingColumn, spec, visitState, allNodes, overrideMap);
        }

        if (endingColumn.nodes.Count > 0)
        {
            endingColumn.columnHeader = endingColumn.nodes.Count > 1 ? "ENDINGS" : "ENDING";
            model.endingColumn = endingColumn;
        }
        else
        {
            model.endingColumn = null;
        }

        // 7. Build explicit directed edges across playthroughs & compute slot discovery metrics
        BuildModelEdges(model, prevEdgeSet);

        HashSet<string> combinedDiscovered = new HashSet<string>(prevNodeSet, StringComparer.Ordinal);
        CollectCurrentRunKeys(out HashSet<string> currentRunKeys, out _);
        combinedDiscovered.UnionWith(currentRunKeys);

        int uniqueDiscoverableCount = 0;
        foreach (string key in combinedDiscovered)
        {
            if (IsDiscoverableKey(key)) uniqueDiscoverableCount++;
        }

        model.totalDiscoverableNodeCount = GetTotalDiscoverableNodeCount(allNodes);
        model.discoveredNodeCount = uniqueDiscoverableCount;
        model.discoveryPercent = ComputeDiscoveryPercentage(combinedDiscovered, allNodes);

        return model;
    }

    private static bool HasAnyOptionInSet(int parentNodeId, int optionCount, HashSet<string> nodeSet)
    {
        if (nodeSet == null || nodeSet.Count == 0)
        {
            return false;
        }

        for (int o = 0; o < optionCount; o++)
        {
            if (nodeSet.Contains(MakeDecisionNodeKey(parentNodeId, o)))
            {
                return true;
            }
        }
        return false;
    }

    private static void BuildModelEdges(ScenarioTreeModel model, HashSet<string> prevEdgeSet)
    {
        if (model == null) return;

        List<ScenarioColumnData> storyCols = new List<ScenarioColumnData>();
        if (model.rootColumn != null) storyCols.Add(model.rootColumn);
        if (model.decisionColumns != null) storyCols.AddRange(model.decisionColumns);

        Dictionary<string, ScenarioOptionNodeData> nodeLookup = new Dictionary<string, ScenarioOptionNodeData>(StringComparer.Ordinal);
        List<ScenarioColumnData> allCols = new List<ScenarioColumnData>(storyCols);
        if (model.endingColumn != null) allCols.Add(model.endingColumn);

        for (int c = 0; c < allCols.Count; c++)
        {
            List<ScenarioOptionNodeData> nodes = allCols[c].nodes;
            for (int n = 0; n < nodes.Count; n++)
            {
                if (nodes[n] != null && !string.IsNullOrEmpty(nodes[n].nodeKey))
                {
                    nodeLookup[nodes[n].nodeKey] = nodes[n];
                }
            }
        }

        Dictionary<string, ScenarioEdgeData> edgeMap = new Dictionary<string, ScenarioEdgeData>(StringComparer.Ordinal);

        void RegisterEdge(string fromKey, string toKey, ScenarioNodeVisitState state, bool isEnding)
        {
            if (string.IsNullOrEmpty(fromKey) || string.IsNullOrEmpty(toKey)) return;
            if (!nodeLookup.ContainsKey(fromKey) || !nodeLookup.ContainsKey(toKey)) return;

            string key = MakeEdgeKey(fromKey, toKey);
            if (edgeMap.TryGetValue(key, out ScenarioEdgeData existing))
            {
                if ((int)state > (int)existing.visitState)
                {
                    existing.visitState = state;
                }
                existing.isEndingConnection |= isEnding;
                return;
            }

            ScenarioEdgeData edge = new ScenarioEdgeData
            {
                fromNodeKey = fromKey,
                toNodeKey = toKey,
                visitState = state,
                isEndingConnection = isEnding
            };
            edgeMap[key] = edge;
            model.edges.Add(edge);
        }

        // A. Connect current-run spine (including reached ending if completed)
        List<ScenarioOptionNodeData> currentRunSpine = new List<ScenarioOptionNodeData>();
        for (int c = 0; c < storyCols.Count; c++)
        {
            ScenarioOptionNodeData currentInCol = FindNodeWithState(storyCols[c].nodes, ScenarioNodeVisitState.CurrentRun);
            if (currentInCol != null)
            {
                currentRunSpine.Add(currentInCol);
            }
        }
        if (model.endingColumn != null)
        {
            ScenarioOptionNodeData currentEnding = FindNodeWithState(model.endingColumn.nodes, ScenarioNodeVisitState.CurrentRun);
            if (currentEnding != null)
            {
                currentRunSpine.Add(currentEnding);
            }
        }

        for (int i = 0; i + 1 < currentRunSpine.Count; i++)
        {
            RegisterEdge(
                currentRunSpine[i].nodeKey,
                currentRunSpine[i + 1].nodeKey,
                ScenarioNodeVisitState.CurrentRun,
                currentRunSpine[i + 1].isEndingNode);
        }

        // B. Restore exact traversed edges from previous playthroughs in this save slot
        if (prevEdgeSet != null && prevEdgeSet.Count > 0)
        {
            foreach (string rawEdge in prevEdgeSet)
            {
                if (string.IsNullOrEmpty(rawEdge)) continue;
                int sep = rawEdge.IndexOf("->", StringComparison.Ordinal);
                if (sep <= 0) continue;

                string fromKey = rawEdge.Substring(0, sep);
                string toKey = rawEdge.Substring(sep + 2);
                if (nodeLookup.TryGetValue(toKey, out ScenarioOptionNodeData toNode))
                {
                    RegisterEdge(fromKey, toKey, ScenarioNodeVisitState.PreviousRun, toNode.isEndingNode);
                }
            }
        }

        // C. Connect undiscovered ("???") sibling options cleanly from adjacent column's active or previously-taken node
        for (int c = 0; c + 1 < storyCols.Count; c++)
        {
            ScenarioOptionNodeData parentSource =
                FindNodeWithState(storyCols[c].nodes, ScenarioNodeVisitState.CurrentRun) ??
                FindNodeWithState(storyCols[c].nodes, ScenarioNodeVisitState.PreviousRun);

            if (parentSource == null) continue;

            List<ScenarioOptionNodeData> nextNodes = storyCols[c + 1].nodes;
            for (int n = 0; n < nextNodes.Count; n++)
            {
                ScenarioOptionNodeData target = nextNodes[n];
                if (target == null) continue;

                if (target.visitState == ScenarioNodeVisitState.Undiscovered)
                {
                    RegisterEdge(parentSource.nodeKey, target.nodeKey, ScenarioNodeVisitState.Undiscovered, false);
                }
                else if (target.visitState == ScenarioNodeVisitState.PreviousRun &&
                         !HasIncomingEdge(target.nodeKey, model.edges))
                {
                    // Legacy save fallback if edge list was not yet recorded
                    RegisterEdge(parentSource.nodeKey, target.nodeKey, ScenarioNodeVisitState.PreviousRun, false);
                }
            }
        }

        // D. Ensure any previous ending without explicit saved edge links from the last story column's previous/current node
        if (model.endingColumn != null && storyCols.Count > 0)
        {
            List<ScenarioOptionNodeData> lastStoryNodes = storyCols[storyCols.Count - 1].nodes;
            for (int e = 0; e < model.endingColumn.nodes.Count; e++)
            {
                ScenarioOptionNodeData endNode = model.endingColumn.nodes[e];
                if (endNode == null || HasIncomingEdge(endNode.nodeKey, model.edges)) continue;

                ScenarioOptionNodeData source =
                    FindNodeWithState(lastStoryNodes, endNode.visitState) ??
                    FindNodeWithState(lastStoryNodes, ScenarioNodeVisitState.PreviousRun) ??
                    FindNodeWithState(lastStoryNodes, ScenarioNodeVisitState.CurrentRun);

                if (source != null)
                {
                    RegisterEdge(source.nodeKey, endNode.nodeKey, endNode.visitState, true);
                }
            }
        }
    }

    private static ScenarioOptionNodeData FindNodeWithState(List<ScenarioOptionNodeData> nodes, ScenarioNodeVisitState state)
    {
        if (nodes == null) return null;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].visitState == state)
            {
                return nodes[i];
            }
        }
        return null;
    }

    private static bool HasIncomingEdge(string targetKey, List<ScenarioEdgeData> edges)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            if (string.Equals(edges[i].toNodeKey, targetKey, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void AddDiscoveredEndingNode(
        ScenarioColumnData endingColumn,
        EndingSpec spec,
        ScenarioNodeVisitState visitState,
        IReadOnlyDictionary<int, NarrativeNode> allNodes,
        Dictionary<int, ScenarioNodeOverride> overrideMap)
    {
        NarrativeNode endingNode = null;
        if (allNodes != null)
        {
            allNodes.TryGetValue(spec.id, out endingNode);
        }

        ScenarioOverridePair ov = GetNodeLevelOverride(spec.id, endingNode, overrideMap);
        string title = !string.IsNullOrWhiteSpace(ov.title) ? ov.title : spec.defaultTitle;
        string label = !string.IsNullOrWhiteSpace(ov.title) ? ov.title : spec.defaultLabel;
        string annotation = !string.IsNullOrWhiteSpace(ov.annotation) ? ov.annotation : spec.defaultAnnotation;

        if (string.IsNullOrWhiteSpace(ov.annotation) && endingNode != null && endingNode.Dialogues() != null && endingNode.Dialogues().Count > 1)
        {
            string summaryLine = endingNode.Dialogues()[1].Content();
            if (!string.IsNullOrWhiteSpace(summaryLine))
            {
                annotation = $"{spec.defaultAnnotation}\n\n\"{summaryLine}\"";
            }
        }

        ResolvedNodeVisuals visuals = ResolveNodeVisualOverrides(
            spec.id, visitState, overrideMap, null, endingNode);

        endingColumn.nodes.Add(new ScenarioOptionNodeData
        {
            nodeKey = MakeEndingNodeKey(spec.id),
            parentNodeId = spec.id,
            optionIndex = spec.optionIndex,
            resolvedNextNodeId = -1,
            storyDay = 5,
            stageTitle = title,
            revealedLabel = label,
            revealedAnnotation = annotation,
            visitState = visitState,
            isTaken = visitState == ScenarioNodeVisitState.CurrentRun,
            isRootNode = false,
            isEndingNode = true,
            endingNodeId = spec.id,
            customSprite = visuals.customSprite,
            situationImage = visuals.situationImage,
            hasCustomGraphPosition = visuals.useCustomGraphPosition,
            graphPosition = visuals.customGraphPosition
        });
    }

    private struct ResolvedNodeVisuals
    {
        public Sprite customSprite;
        public Sprite situationImage;
        public bool useCustomGraphPosition;
        public Vector2 customGraphPosition;
    }

    private static ResolvedNodeVisuals ResolveNodeVisualOverrides(
        int nodeId,
        ScenarioNodeVisitState visitState,
        Dictionary<int, ScenarioNodeOverride> overrideMap,
        Option rawOption = null,
        NarrativeNode narrativeNode = null)
    {
        ResolvedNodeVisuals resolved = default;
        if (overrideMap != null && overrideMap.TryGetValue(nodeId, out ScenarioNodeOverride ov) && ov != null)
        {
            resolved.customSprite = visitState != ScenarioNodeVisitState.Undiscovered
                ? ov.customTakenSprite
                : ov.customUntakenSprite;
            resolved.situationImage = ov.detailSituationImage;
            resolved.useCustomGraphPosition = ov.useCustomGraphPosition;
            resolved.customGraphPosition = ov.customGraphPosition;
        }

        if (resolved.customSprite == null && rawOption != null)
        {
            resolved.customSprite = rawOption.GetScenarioCustomSprite();
        }
        if (resolved.customSprite == null && narrativeNode != null)
        {
            resolved.customSprite = narrativeNode.ScenarioCustomSprite();
        }

        return resolved;
    }

    private struct ScenarioOverridePair
    {
        public string title;
        public string annotation;
    }

    private static ScenarioOverridePair GetNodeLevelOverride(
        int nodeId,
        NarrativeNode node,
        Dictionary<int, ScenarioNodeOverride> overrideMap)
    {
        ScenarioOverridePair result = new ScenarioOverridePair
        {
            title = node != null ? node.ScenarioTitle() : string.Empty,
            annotation = node != null ? node.ScenarioAnnotation() : string.Empty
        };

        if (overrideMap != null && overrideMap.TryGetValue(nodeId, out ScenarioNodeOverride ov) && ov != null)
        {
            if (!string.IsNullOrWhiteSpace(ov.customTitle))
            {
                result.title = ov.customTitle;
            }
            if (!string.IsNullOrWhiteSpace(ov.customAnnotation))
            {
                result.annotation = ov.customAnnotation;
            }
        }

        return result;
    }

    private static bool ShouldShowNodeInScenarioTree(
        int nodeId,
        IReadOnlyDictionary<int, NarrativeNode> allNodes,
        Dictionary<int, ScenarioNodeOverride> overrideMap)
    {
        if (overrideMap != null && overrideMap.TryGetValue(nodeId, out ScenarioNodeOverride ov) && ov != null)
        {
            return ov.showInScenarioTree;
        }

        if (allNodes != null && allNodes.TryGetValue(nodeId, out NarrativeNode node) && node != null)
        {
            return node.ShowInScenarioTree();
        }

        return true;
    }

    /// <summary>
    /// Checks whether Luna has downloaded the Connect application from the App Store.
    /// </summary>
    public static bool IsConnectAppDownloaded()
    {
        // 1. Check UIManager live state
        if (UIManager.Instance != null && UIManager.Instance.IsConnectInstalled)
        {
            return true;
        }

        // 2. If the campaign ending is reached, Connect was definitely downloaded
        if (IsCampaignCompleted())
        {
            return true;
        }

        // 3. If the game has progressed past Day 1 (Day 2..5), Connect has already been downloaded
        if (DayManager.Instance != null && DayManager.Instance.GetCurrentDay() >= 2)
        {
            return true;
        }

        // 4. If Choice 1 (Connect profile) or later choice has been made, Connect was downloaded
        if (DayManager.Instance != null && DayManager.Instance.TryGetRecordedChoice(1, out _))
        {
            return true;
        }

        if (DecisionDataManager.Instance != null &&
            (DecisionDataManager.Instance.TryGetIntVariable("Scenario_Node_0", out _) ||
             DecisionDataManager.Instance.TryGetIntVariable("Scenario_Choice_1", out _)))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetExplicitEndingNodeId(out int endingNodeId)
    {
        // 1. Check DayManager last resolved ending node
        if (DayManager.Instance != null && IsCanonicalEndingNodeId(DayManager.Instance.LastResolvedEndingNode))
        {
            endingNodeId = DayManager.Instance.LastResolvedEndingNode;
            return true;
        }

        // 2. Check DecisionDataManager for saved ending node
        if (DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable("Scenario_EndingNode", out int savedEnding) &&
            IsCanonicalEndingNodeId(savedEnding))
        {
            endingNodeId = savedEnding;
            return true;
        }

        // 3. Check MessagesManager current contact (Riley completed campaign)
        if (MessagesManager.Instance != null)
        {
            Contact riley = MessagesManager.Instance.GetContactByName("Riley");
            if (riley != null && IsCanonicalEndingNodeId(riley.NarrativeNode()))
            {
                endingNodeId = riley.NarrativeNode();
                return true;
            }
        }

        endingNodeId = -1;
        return false;
    }

    /// <summary>
    /// Checks whether the campaign ending sequence has been reached and resolved.
    /// </summary>
    public static bool IsCampaignCompleted()
    {
        return TryGetExplicitEndingNodeId(out _);
    }

    private static int ResolveSelectedOptionIndex(
        CampaignDecisionSpec spec,
        IReadOnlyDictionary<int, NarrativeNode> allNodes)
    {
        // 1. Check DecisionDataManager for explicit node selection recorded by OptionsChanger
        if (DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable($"Scenario_Node_{spec.nodeId}", out int recordedOption) &&
            recordedOption >= 0)
        {
            return recordedOption;
        }

        // 2. Check DayManager's recorded choice route (from EventBus ChoiceX_Safe/Neutral/Risky or saved Scenario_Choice_X)
        if (DayManager.Instance != null &&
            DayManager.Instance.TryGetRecordedChoice(spec.choiceNumber, out DayManager.TestRoute route))
        {
            return route switch
            {
                DayManager.TestRoute.Safe => 0,
                DayManager.TestRoute.Neutral => 1,
                DayManager.TestRoute.Risky => 2,
                _ => 1
            };
        }

        // 3. Inspect contact's current narrative node & chat history in MessagesManager
        if (MessagesManager.Instance != null)
        {
            Contact contact = MessagesManager.Instance.GetContactByName(spec.contactName);
            if (contact != null)
            {
                int currentContactNode = contact.NarrativeNode();
                if (currentContactNode == spec.safeBranchNode) return 0;
                if (currentContactNode == spec.neutralBranchNode) return 1;
                if (currentContactNode == spec.riskyBranchNode) return 2;
            }

            List<Message> history = MessagesManager.Instance.GetContactMessages(spec.contactName);
            if (history != null && history.Count > 0 && allNodes != null)
            {
                if (HistoryContainsBranchDialogue(history, allNodes, spec.safeBranchNode)) return 0;
                if (HistoryContainsBranchDialogue(history, allNodes, spec.neutralBranchNode)) return 1;
                if (HistoryContainsBranchDialogue(history, allNodes, spec.riskyBranchNode)) return 2;
            }
        }

        // If the decision has not been taken yet by the player, return -1 (not taken)
        return -1;
    }

    private static int ResolveCustomNodeSelectedOption(int nodeId)
    {
        if (DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable($"Scenario_Node_{nodeId}", out int selectedOpt))
        {
            return selectedOpt;
        }

        return -1;
    }

    private static bool HistoryContainsBranchDialogue(
        List<Message> history,
        IReadOnlyDictionary<int, NarrativeNode> allNodes,
        int branchNodeId)
    {
        if (branchNodeId < 0 || !allNodes.TryGetValue(branchNodeId, out NarrativeNode branchNode) || branchNode == null)
        {
            return false;
        }

        List<Message> branchDialogues = branchNode.Dialogues();
        if (branchDialogues == null || branchDialogues.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < branchDialogues.Count; i++)
        {
            Message branchMsg = branchDialogues[i];
            if (branchMsg == null || MessagesManager.IsNarration(branchMsg) || string.IsNullOrWhiteSpace(branchMsg.Content()))
            {
                continue;
            }

            for (int h = 0; h < history.Count; h++)
            {
                if (history[h] != null && string.Equals(history[h].Content(), branchMsg.Content(), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int ResolveReachedEndingNodeId(IReadOnlyDictionary<int, NarrativeNode> allNodes)
    {
        if (TryGetExplicitEndingNodeId(out int explicitEnding))
        {
            return explicitEnding;
        }

        // Fallback: compute from DayManager risk score (or Reputation)
        if (DayManager.Instance != null)
        {
            int modifier = 0;
            if (DayManager.Instance.TryGetRecordedChoice(13, out DayManager.TestRoute choice13))
            {
                modifier = choice13 switch
                {
                    DayManager.TestRoute.Safe => -3,
                    DayManager.TestRoute.Risky => 3,
                    _ => 0
                };
            }

            int finalScore = DayManager.Instance.NarrativeRiskScore + modifier;
            if (finalScore <= -4) return GoodEndingNodeId;
            if (finalScore >= 5) return BadEndingNodeId;
            return NeutralEndingNodeId;
        }

        if (ReputationManager.Instance != null)
        {
            int rep = ReputationManager.Instance.CurrentReputation;
            if (rep >= 65) return GoodEndingNodeId;
            if (rep <= 35) return BadEndingNodeId;
        }

        return NeutralEndingNodeId;
    }

    private static int GetSpecBranchNode(CampaignDecisionSpec spec, int optionIndex)
    {
        return optionIndex switch
        {
            0 => spec.safeBranchNode,
            1 => spec.neutralBranchNode,
            2 => spec.riskyBranchNode,
            _ => -1
        };
    }

    private static string GetSpecOptionLabel(CampaignDecisionSpec spec, int optionIndex, Option rawOption, int totalOptions = 3)
    {
        if (rawOption != null && !string.IsNullOrWhiteSpace(rawOption.GetScenarioLabel()))
        {
            return rawOption.GetScenarioLabel();
        }

        // If the decision has an unconventional number of options (e.g. 2 instead of 3), use the option's text
        if (totalOptions != 3 && rawOption != null && !string.IsNullOrWhiteSpace(rawOption.text))
        {
            return FormatShortOptionText(rawOption.text);
        }

        return optionIndex switch
        {
            0 => spec.safeShortLabel,
            1 => spec.neutralShortLabel,
            2 => spec.riskyShortLabel,
            _ => rawOption != null ? FormatShortOptionText(rawOption.text) : "Decision\nMade"
        };
    }

    private static string GetSpecOptionAnnotation(
        CampaignDecisionSpec spec,
        int optionIndex,
        Option rawOption,
        string nodeLevelAnnotationOverride,
        int totalOptions = 3)
    {
        if (rawOption != null && !string.IsNullOrWhiteSpace(rawOption.GetScenarioAnnotation()))
        {
            return rawOption.GetScenarioAnnotation();
        }

        if (!string.IsNullOrWhiteSpace(nodeLevelAnnotationOverride))
        {
            return nodeLevelAnnotationOverride;
        }

        // If the decision has an unconventional number of options (e.g. 2 instead of 3), use the option's text directly
        if (totalOptions != 3 && rawOption != null && !string.IsNullOrWhiteSpace(rawOption.text))
        {
            return rawOption.text;
        }

        string baseNote = optionIndex switch
        {
            0 => spec.safeAnnotation,
            1 => spec.neutralAnnotation,
            2 => spec.riskyAnnotation,
            _ => rawOption != null ? rawOption.text : "Decision taken during the story."
        };

        if (rawOption != null && !string.IsNullOrWhiteSpace(rawOption.text))
        {
            return $"{baseNote}\n\nChoice selected: \"{rawOption.text}\"";
        }

        return baseNote;
    }

    private static string FormatShortOptionText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return "Decision\nMade";
        }

        string trimmed = rawText.Trim();
        if (trimmed.Length <= 28)
        {
            return trimmed;
        }

        return trimmed.Substring(0, 25) + "...";
    }

    private static string GetDefaultFreeTextRouteLabel(int optionIndex)
    {
        return optionIndex switch
        {
            0 => "Safe Reply",
            1 => "Neutral Reply",
            2 => "Risky Reply",
            _ => "Reply"
        };
    }

    private static int EstimateStoryDay(NarrativeNode node)
    {
        if (node != null)
        {
            if (node.Dialogues() != null && node.Dialogues().Count > 0)
            {
                Message first = node.Dialogues()[0];
                if (first != null && first.Timestamp().day >= 1 && first.Timestamp().day <= 5)
                {
                    return first.Timestamp().day;
                }
            }

            Option[] options = node.Options();
            if (options != null)
            {
                for (int i = 0; i < options.Length; i++)
                {
                    if (options[i] != null && options[i].messagesToSend != null && options[i].messagesToSend.Length > 0)
                    {
                        Message firstSent = options[i].messagesToSend[0];
                        if (firstSent != null && firstSent.Timestamp().day >= 1 && firstSent.Timestamp().day <= 5)
                        {
                            return firstSent.Timestamp().day;
                        }
                    }
                }
            }
        }
        return 1;
    }
}
