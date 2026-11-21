using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps a NarrativeNode ID (and optional option index) to a situation illustration shown
/// on the left panel of the 70% ScenarioSystem node detail modal.
/// </summary>
[Serializable]
public class ScenarioSituationImageEntry
{
    [Tooltip("NarrativeNode ID (-100 = Start/Root, 0..67 = Campaign decisions, 71..73 = Endings).")]
    public int narrativeNodeId;

    [Tooltip("-1 applies to the whole situation regardless of option chosen; >= 0 overrides a specific option index.")]
    public int optionIndex = -1;

    [Tooltip("Optional designer-friendly label (e.g., 'Day 1 - Connect Profile').")]
    public string label;

    [Tooltip("Representative sprite displayed on the left side of the 70% detail screen.")]
    public Sprite situationImage;
}

/// <summary>
/// ScriptableObject catalog for assigning situation illustrations to ScenarioSystem nodes.
/// Create via Assets -> Create -> Before You Send -> Scenario Situation Image Catalog.
/// </summary>
[CreateAssetMenu(
    fileName = "ScenarioSituationImageCatalog",
    menuName = "Before You Send/Scenario Situation Image Catalog")]
public class ScenarioSituationImageCatalog : ScriptableObject
{
    [Tooltip("Entries mapping scenario decision nodes and endings to representative illustrations.")]
    public List<ScenarioSituationImageEntry> entries = new List<ScenarioSituationImageEntry>();

    /// <summary>
    /// Resolves a sprite for the given narrativeNodeId and optionIndex from this catalog.
    /// Exact (nodeId, optionIndex) matches take priority over situation-wide (nodeId, -1) entries.
    /// </summary>
    public Sprite ResolveImage(int narrativeNodeId, int optionIndex)
    {
        return ResolveFromEntries(entries, narrativeNodeId, optionIndex);
    }

    /// <summary>
    /// Shared lookup helper usable by both ScenarioSituationImageCatalog assets and inline lists.
    /// </summary>
    public static Sprite ResolveFromEntries(
        IReadOnlyList<ScenarioSituationImageEntry> entryList,
        int narrativeNodeId,
        int optionIndex)
    {
        if (entryList == null || entryList.Count == 0)
        {
            return null;
        }

        Sprite situationWideMatch = null;
        for (int i = 0; i < entryList.Count; i++)
        {
            ScenarioSituationImageEntry entry = entryList[i];
            if (entry == null || entry.situationImage == null || entry.narrativeNodeId != narrativeNodeId)
            {
                continue;
            }

            if (entry.optionIndex == optionIndex)
            {
                return entry.situationImage;
            }

            if (entry.optionIndex < 0 && situationWideMatch == null)
            {
                situationWideMatch = entry.situationImage;
            }
        }

        return situationWideMatch;
    }

    /// <summary>
    /// Fallback loader checking Resources/ScenarioImages/Node_{id}_Opt_{idx}.png and Node_{id}.png.
    /// Lets designers drop images in Resources/ScenarioImages/ with zero Inspector wiring required.
    /// </summary>
    public static Sprite ResolveFallbackFromResources(int narrativeNodeId, int optionIndex)
    {
        if (optionIndex >= 0)
        {
            Sprite optSprite = Resources.Load<Sprite>($"ScenarioImages/Node_{narrativeNodeId}_Opt_{optionIndex}");
            if (optSprite != null)
            {
                return optSprite;
            }
        }

        Sprite nodeSprite = Resources.Load<Sprite>($"ScenarioImages/Node_{narrativeNodeId}");
        if (nodeSprite != null)
        {
            return nodeSprite;
        }

        if (narrativeNodeId == -100)
        {
            return Resources.Load<Sprite>("ScenarioImages/Root");
        }

        return null;
    }

    [ContextMenu("Populate Missing Default Entries")]
    public void PopulateMissingDefaultEntries()
    {
        List<(int nodeId, string title)> defaults = ScenarioDataCollector.GetDefaultSituationCatalogEntries();

        for (int i = 0; i < defaults.Count; i++)
        {
            bool exists = false;
            for (int e = 0; e < entries.Count; e++)
            {
                if (entries[e] != null && entries[e].narrativeNodeId == defaults[i].nodeId && entries[e].optionIndex < 0)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                entries.Add(new ScenarioSituationImageEntry
                {
                    narrativeNodeId = defaults[i].nodeId,
                    optionIndex = -1,
                    label = defaults[i].title,
                    situationImage = null
                });
            }
        }
    }
}
