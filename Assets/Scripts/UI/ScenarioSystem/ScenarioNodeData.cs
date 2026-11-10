using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines how untaken narrative branches are displayed in the endgame ScenarioSystem flowchart.
/// </summary>
public enum ScenarioBranchMode
{
    /// <summary>
    /// Option B (Preferred): Shows the first node of an untaken branch with "???" (both in the node and on hover),
    /// hides child/future nodes that depend exclusively on that untaken branch, and draws outgoing arrows pointing into the void.
    /// </summary>
    OptionB_FirstNodeWithVoidArrows = 0,

    /// <summary>
    /// Option A: Shows all nodes of untaken branches with "???" inside the node and "???" in the hover annotation box.
    /// </summary>
    OptionA_ShowAllNodesAsHidden = 1
}

/// <summary>
/// Inspector-configurable override allowing designers to toggle whether a specific NarrativeNode appears
/// in the endgame ScenarioSystem summary screen and optionally customize its title or hover annotation.
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

    [Tooltip("Optional override for the hover annotation box when this node was taken.")]
    [TextArea(2, 4)]
    public string customAnnotation;

    [Tooltip("Optional custom sprite for this specific node when taken/revealed.")]
    public Sprite customTakenSprite;

    [Tooltip("Optional custom sprite for this specific node when untaken ('???').")]
    public Sprite customUntakenSprite;
}

/// <summary>
/// Runtime view-model for a single node box inside the ScenarioSystem flowchart.
/// </summary>
[Serializable]
public class ScenarioOptionNodeData
{
    public int parentNodeId;
    public int optionIndex;
    public int resolvedNextNodeId;
    public int storyDay;
    public string stageTitle;
    public string revealedLabel;
    public string revealedAnnotation;
    public bool isTaken;
    public bool isRootNode;
    public bool isEndingNode;
    public int endingNodeId;
    public bool hasOutgoingVoidArrows;
    public int voidArrowCount;
    public Sprite customSprite;

    /// <summary>
    /// Returns the text to display inside the node box according to whether the player took this decision.
    /// Untaken nodes always display "???".
    /// </summary>
    public string GetDisplayLabel()
    {
        if (!isTaken)
        {
            return "???";
        }

        return string.IsNullOrWhiteSpace(revealedLabel) ? "Decision\nMade" : revealedLabel;
    }

    /// <summary>
    /// Returns the text to display inside the hover annotation tooltip according to whether the player took this decision.
    /// Untaken nodes always display "???".
    /// </summary>
    public string GetDisplayAnnotation()
    {
        if (!isTaken)
        {
            return "???";
        }

        return string.IsNullOrWhiteSpace(revealedAnnotation)
            ? "You selected this narrative path during your playthrough."
            : revealedAnnotation;
    }
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
    public int finalReputation = 50;
    public int narrativeRiskScore = 0;
    public int reachedEndingNodeId = 72;
    public string reachedEndingTitle = "NEUTRAL ENDING";
    public bool isCampaignCompleted = false;
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

    /// <summary>
    /// Builds the complete ScenarioTreeModel by inspecting the current state of DayManager, DecisionDataManager,
    /// ReputationManager, and MessagesManager, filtered by NarrativeNode.ShowInScenarioTree() and Inspector overrides.
    /// </summary>
    public static ScenarioTreeModel CollectTreeModel(
        ScenarioBranchMode branchMode,
        List<ScenarioNodeOverride> inspectorOverrides = null)
    {
        ScenarioTreeModel model = new ScenarioTreeModel();

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

        // 2. The initial story root node ("Luna joins Connect") only appears once the Connect application has been downloaded
        if (isConnectDownloaded)
        {
            model.rootColumn = new ScenarioColumnData
            {
                columnIndex = 0,
                narrativeNodeId = -100,
                choiceNumber = 0,
                storyDay = 1,
                columnHeader = "Start",
                isRootEntryColumn = true,
                isEndingColumn = false
            };

            string rootTitle = "Day 1\nConnect Installed";
            string rootAnnotation = "Start of 'Before You Send': Luna joins the Connect social network at her new school. Every decision shapes her digital footprint and safety across the 5 days.";
            if (overrideMap.TryGetValue(-100, out ScenarioNodeOverride rootOverride))
            {
                if (!string.IsNullOrWhiteSpace(rootOverride.customTitle)) rootTitle = rootOverride.customTitle;
                if (!string.IsNullOrWhiteSpace(rootOverride.customAnnotation)) rootAnnotation = rootOverride.customAnnotation;
            }

            model.rootColumn.nodes.Add(new ScenarioOptionNodeData
            {
                parentNodeId = -100,
                optionIndex = 0,
                resolvedNextNodeId = 0,
                storyDay = 1,
                stageTitle = "Campaign Start",
                revealedLabel = rootTitle,
                revealedAnnotation = rootAnnotation,
                isTaken = true,
                isRootNode = true,
                isEndingNode = false,
                hasOutgoingVoidArrows = false,
                voidArrowCount = 0
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

            // Under Option B, if Choice 7 was Safe (refused to send photo), Choice 8 (Node 40 - Leaked Photo Crisis)
            // belongs exclusively to the untaken Neutral/Risky branches of Choice 7 and is hidden completely!
            if (spec.dependsOnChoice7NonSafe && choice7WasSafe && branchMode == ScenarioBranchMode.OptionB_FirstNodeWithVoidArrows)
            {
                continue;
            }

            bool isSkippedBranchUnderOptionA = isCampaignCompleted && spec.dependsOnChoice7NonSafe && choice7WasSafe && branchMode == ScenarioBranchMode.OptionA_ShowAllNodesAsHidden;
            int selectedOptionIndex = isSkippedBranchUnderOptionA ? -1 : ResolveSelectedOptionIndex(spec, allNodes);

            // In-game preview before campaign completion (e.g. F10 during gameplay): only display decisions already taken!
            if (selectedOptionIndex < 0 && !isSkippedBranchUnderOptionA)
            {
                continue;
            }

            NarrativeNode narrativeNode = null;
            if (allNodes != null)
            {
                allNodes.TryGetValue(spec.nodeId, out narrativeNode);
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
                isSkippedBranchColumn = isSkippedBranchUnderOptionA
            };

            Option[] rawOptions = narrativeNode != null ? narrativeNode.Options() : null;
            int optionCount = (rawOptions != null && rawOptions.Length > 0) ? rawOptions.Length : 3;

            for (int optIdx = 0; optIdx < optionCount; optIdx++)
            {
                Option rawOpt = (rawOptions != null && optIdx < rawOptions.Length) ? rawOptions[optIdx] : null;
                if (rawOpt != null && !rawOpt.ShowInScenarioTree())
                {
                    continue;
                }

                bool isOptionTaken = (selectedOptionIndex == optIdx);
                string defaultLabel = GetSpecOptionLabel(spec, optIdx, rawOpt, optionCount);
                string defaultAnnotation = GetSpecOptionAnnotation(spec, optIdx, rawOpt, nodeLevelOverride.annotation, optionCount);

                int nextNodeId = rawOpt != null ? rawOpt.nextNode : GetSpecBranchNode(spec, optIdx);

                // Check for custom per-node / per-option sprites configured in inspector or NarrativeNode
                Sprite customOptSprite = null;
                if (overrideMap != null && overrideMap.TryGetValue(spec.nodeId, out ScenarioNodeOverride specOv) && specOv != null)
                {
                    customOptSprite = isOptionTaken ? specOv.customTakenSprite : specOv.customUntakenSprite;
                }
                if (customOptSprite == null && rawOpt != null)
                {
                    customOptSprite = rawOpt.GetScenarioCustomSprite();
                }
                if (customOptSprite == null && narrativeNode != null)
                {
                    customOptSprite = narrativeNode.ScenarioCustomSprite();
                }

                // Under Option B, untaken decision nodes are the first node of the unchosen path ("???")
                // and their outgoing connection lines point into the void (empty space).
                bool showVoidArrows = !isOptionTaken && branchMode == ScenarioBranchMode.OptionB_FirstNodeWithVoidArrows;

                column.nodes.Add(new ScenarioOptionNodeData
                {
                    parentNodeId = spec.nodeId,
                    optionIndex = optIdx,
                    resolvedNextNodeId = nextNodeId,
                    storyDay = spec.day,
                    stageTitle = column.columnHeader,
                    revealedLabel = defaultLabel,
                    revealedAnnotation = defaultAnnotation,
                    isTaken = isOptionTaken,
                    isRootNode = false,
                    isEndingNode = false,
                    hasOutgoingVoidArrows = showVoidArrows,
                    voidArrowCount = showVoidArrows ? 2 : 0,
                    customSprite = customOptSprite
                });
            }

            if (column.nodes.Count > 0)
            {
                model.decisionColumns.Add(column);
            }
        }

        // 5. Also include any custom/additional NarrativeNodes added by designers that have _hasOptions == true and _showInScenarioTree == true
        // (Excluding Node 4 which is the legacy unused duplicate of Node 9, and excluding ending nodes 71, 72, 73)
        handledNodeIds.Add(4);
        handledNodeIds.Add(71);
        handledNodeIds.Add(72);
        handledNodeIds.Add(73);

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
                int selectedOpt = ResolveCustomNodeSelectedOption(extraNodeId);

                // If this custom decision node has not been reached/selected by the player yet, skip it
                if (selectedOpt < 0)
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

                Option[] opts = extraNode.Options();
                for (int optIdx = 0; optIdx < opts.Length; optIdx++)
                {
                    Option opt = opts[optIdx];
                    if (opt == null || !opt.ShowInScenarioTree()) continue;

                    bool taken = (selectedOpt == optIdx);
                    string label = !string.IsNullOrWhiteSpace(opt.GetScenarioLabel())
                        ? opt.GetScenarioLabel()
                        : FormatShortOptionText(opt.text);
                    string annotation = !string.IsNullOrWhiteSpace(opt.GetScenarioAnnotation())
                        ? opt.GetScenarioAnnotation()
                        : (!string.IsNullOrWhiteSpace(extraOverride.annotation) ? extraOverride.annotation : opt.text);

                    bool showVoidArrows = !taken && branchMode == ScenarioBranchMode.OptionB_FirstNodeWithVoidArrows;

                    Sprite customOptSprite = null;
                    if (overrideMap != null && overrideMap.TryGetValue(extraNodeId, out ScenarioNodeOverride extraOv) && extraOv != null)
                    {
                        customOptSprite = taken ? extraOv.customTakenSprite : extraOv.customUntakenSprite;
                    }
                    if (customOptSprite == null && opt != null)
                    {
                        customOptSprite = opt.GetScenarioCustomSprite();
                    }
                    if (customOptSprite == null && extraNode != null)
                    {
                        customOptSprite = extraNode.ScenarioCustomSprite();
                    }

                    extraCol.nodes.Add(new ScenarioOptionNodeData
                    {
                        parentNodeId = extraNodeId,
                        optionIndex = optIdx,
                        resolvedNextNodeId = opt.nextNode,
                        storyDay = extraCol.storyDay,
                        stageTitle = stageHeader,
                        revealedLabel = label,
                        revealedAnnotation = annotation,
                        isTaken = taken,
                        isRootNode = false,
                        isEndingNode = false,
                        hasOutgoingVoidArrows = showVoidArrows,
                        voidArrowCount = showVoidArrows ? 2 : 0,
                        customSprite = customOptSprite
                    });
                }

                if (extraCol.nodes.Count > 0)
                {
                    model.decisionColumns.Add(extraCol);
                }
            }
        }

        // 6. Populate the mandatory 3 Fixed Ending Nodes column only if the campaign is completed
        if (isCampaignCompleted)
        {
            int reachedEndingId = ResolveReachedEndingNodeId(allNodes);
            model.reachedEndingNodeId = reachedEndingId;
            model.reachedEndingTitle = reachedEndingId switch
            {
                71 => "GOOD ENDING",
                73 => "BAD ENDING",
                _ => "NEUTRAL ENDING"
            };

            model.endingColumn = new ScenarioColumnData
            {
                columnIndex = nextColumnIndex,
                narrativeNodeId = reachedEndingId,
                choiceNumber = 14,
                storyDay = 5,
                columnHeader = "Endings",
                isRootEntryColumn = false,
                isEndingColumn = true
            };

            AddFixedEndingNode(
                model.endingColumn,
                71,
                0,
                reachedEndingId == 71,
                "GOOD ENDING\nSafe & Supported",
                "GOOD ENDING (Node 71): Luna protected her boundaries, documented the harassment, and trusted the people around her. The impersonation account was removed and she regained her peace of mind.",
                allNodes,
                overrideMap);

            AddFixedEndingNode(
                model.endingColumn,
                72,
                1,
                reachedEndingId == 72,
                "NEUTRAL ENDING\nLingering Doubt",
                "NEUTRAL ENDING (Node 72): The worst rumors eventually faded, but the leaked posts and unanswered messages left Luna cautious and uneasy every time her phone buzzed.",
                allNodes,
                overrideMap);

            AddFixedEndingNode(
                model.endingColumn,
                73,
                2,
                reachedEndingId == 73,
                "BAD ENDING\nExposed & Overwhelmed",
                "BAD ENDING (Node 73): Oversharing personal information, photos, and credentials snowballed into severe harassment and impersonation that followed Luna into school.",
                allNodes,
                overrideMap);
        }
        else
        {
            model.endingColumn = null;
            model.reachedEndingNodeId = -1;
            model.reachedEndingTitle = "Campaign in Progress";
        }

        return model;
    }

    private static void AddFixedEndingNode(
        ScenarioColumnData endingColumn,
        int endingNodeId,
        int optionIndex,
        bool isReached,
        string defaultLabel,
        string defaultAnnotation,
        IReadOnlyDictionary<int, NarrativeNode> allNodes,
        Dictionary<int, ScenarioNodeOverride> overrideMap)
    {
        NarrativeNode endingNode = null;
        if (allNodes != null)
        {
            allNodes.TryGetValue(endingNodeId, out endingNode);
        }

        ScenarioOverridePair ov = GetNodeLevelOverride(endingNodeId, endingNode, overrideMap);
        string label = !string.IsNullOrWhiteSpace(ov.title) ? ov.title : defaultLabel;
        string annotation = !string.IsNullOrWhiteSpace(ov.annotation) ? ov.annotation : defaultAnnotation;

        // If the NarrativeNode in JSON has narration lines, append the first line for extra context if no custom override was set
        if (string.IsNullOrWhiteSpace(ov.annotation) && endingNode != null && endingNode.Dialogues() != null && endingNode.Dialogues().Count > 1)
        {
            string summaryLine = endingNode.Dialogues()[1].Content();
            if (!string.IsNullOrWhiteSpace(summaryLine))
            {
                annotation = $"{defaultAnnotation}\n\n\"{summaryLine}\"";
            }
        }

        Sprite customEndingSprite = null;
        if (overrideMap != null && overrideMap.TryGetValue(endingNodeId, out ScenarioNodeOverride endingOv) && endingOv != null)
        {
            customEndingSprite = isReached ? endingOv.customTakenSprite : endingOv.customUntakenSprite;
        }
        if (customEndingSprite == null && endingNode != null)
        {
            customEndingSprite = endingNode.ScenarioCustomSprite();
        }

        endingColumn.nodes.Add(new ScenarioOptionNodeData
        {
            parentNodeId = endingNodeId,
            optionIndex = optionIndex,
            resolvedNextNodeId = -1,
            storyDay = 5,
            stageTitle = "Campaign Ending",
            revealedLabel = label,
            revealedAnnotation = annotation,
            isTaken = isReached,
            isRootNode = false,
            isEndingNode = true,
            endingNodeId = endingNodeId,
            hasOutgoingVoidArrows = false,
            voidArrowCount = 0,
            customSprite = customEndingSprite
        });
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

    /// <summary>
    /// Checks whether the campaign ending sequence has been reached and resolved.
    /// </summary>
    public static bool IsCampaignCompleted()
    {
        // 1. Check DayManager last resolved ending node
        if (DayManager.Instance != null &&
            (DayManager.Instance.LastResolvedEndingNode == 71 ||
             DayManager.Instance.LastResolvedEndingNode == 72 ||
             DayManager.Instance.LastResolvedEndingNode == 73))
        {
            return true;
        }

        // 2. Check DecisionDataManager for saved ending node
        if (DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable("Scenario_EndingNode", out int savedEnding) &&
            (savedEnding == 71 || savedEnding == 72 || savedEnding == 73))
        {
            return true;
        }

        // 3. Check MessagesManager current contact (Riley completed campaign)
        if (MessagesManager.Instance != null)
        {
            Contact riley = MessagesManager.Instance.GetContactByName("Riley");
            if (riley != null && (riley.NarrativeNode() == 71 || riley.NarrativeNode() == 72 || riley.NarrativeNode() == 73))
            {
                return true;
            }
        }

        return false;
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
        // 1. Check DayManager's last resolved ending node
        if (DayManager.Instance != null &&
            (DayManager.Instance.LastResolvedEndingNode == 71 ||
             DayManager.Instance.LastResolvedEndingNode == 72 ||
             DayManager.Instance.LastResolvedEndingNode == 73))
        {
            return DayManager.Instance.LastResolvedEndingNode;
        }

        // 2. Check DecisionDataManager saved ending node
        if (DecisionDataManager.Instance != null &&
            DecisionDataManager.Instance.TryGetIntVariable("Scenario_EndingNode", out int savedEnding) &&
            (savedEnding == 71 || savedEnding == 72 || savedEnding == 73))
        {
            return savedEnding;
        }

        // 3. Check Riley's current narrative node in MessagesManager
        if (MessagesManager.Instance != null)
        {
            Contact riley = MessagesManager.Instance.GetContactByName("Riley");
            if (riley != null)
            {
                int rileyNode = riley.NarrativeNode();
                if (rileyNode == 71 || rileyNode == 72 || rileyNode == 73)
                {
                    return rileyNode;
                }
            }
        }

        // 4. Compute from DayManager risk score (or Reputation fallback)
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
            if (finalScore <= -4) return 71;
            if (finalScore >= 5) return 73;
            return 72;
        }

        if (ReputationManager.Instance != null)
        {
            int rep = ReputationManager.Instance.CurrentReputation;
            if (rep >= 65) return 71;
            if (rep <= 35) return 73;
        }

        return 72;
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

    private static int EstimateStoryDay(NarrativeNode node)
    {
        if (node != null && node.Dialogues() != null && node.Dialogues().Count > 0)
        {
            Message first = node.Dialogues()[0];
            if (first != null && first.Timestamp().day >= 1 && first.Timestamp().day <= 5)
            {
                return first.Timestamp().day;
            }
        }
        return 1;
    }
}
