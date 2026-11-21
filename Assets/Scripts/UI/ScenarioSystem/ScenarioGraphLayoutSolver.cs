using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration parameters passed to ScenarioGraphLayoutSolver for computing XY coordinates.
/// </summary>
public struct ScenarioGraphLayoutSettings
{
    public Vector2 decisionNodeSize;
    public Vector2 untakenNodeSize;
    public Vector2 endingNodeSize;
    public float horizontalColumnSpacing;
    public float verticalNodeSpacing;
}

/// <summary>
/// Computed layout metrics returned by ScenarioGraphLayoutSolver.
/// </summary>
public struct ScenarioGraphLayoutResult
{
    public float dividerX;
    public float dividerMinY;
    public float dividerMaxY;
    public Vector2 contentSize;
}

/// <summary>
/// Computes organic 2D XY node positions, overlap resolution, divider placement,
/// and scrollable canvas bounds for the ScenarioSystem flowchart.
/// </summary>
public static class ScenarioGraphLayoutSolver
{
    private const float LeftMarginX = 190f;
    private const float MinNodePadX = 36f;
    private const float MinNodePadY = 34f;
    private const int SkippedLeakChoiceNodeId = 40;

    private static readonly float[] ColumnWaveOffsetsY =
    {
        0f, 55f, -35f, 80f, -65f, 45f, -45f, 95f, -95f, 65f, -55f, 75f, -40f, 30f
    };

    /// <summary>
    /// Returns the display box size for a given scenario node based on its role and discovery state.
    /// </summary>
    public static Vector2 GetNodeBoxSize(
        ScenarioOptionNodeData nodeData,
        bool isEndingCol,
        bool isRootCol,
        ScenarioGraphLayoutSettings settings)
    {
        if (isEndingCol || (nodeData != null && nodeData.isEndingNode))
        {
            return settings.endingNodeSize;
        }

        if (nodeData != null && !nodeData.IsDiscovered && !isRootCol)
        {
            return settings.untakenNodeSize;
        }

        return settings.decisionNodeSize;
    }

    /// <summary>
    /// Computes deterministic 2D XY positions for story columns and endings, resolving overlaps
    /// and calculating the vertical divider and total ScrollRect content dimensions.
    /// </summary>
    public static ScenarioGraphLayoutResult ComputeLayout(
        List<ScenarioColumnData> storyColumns,
        ScenarioColumnData endingColumn,
        ScenarioGraphLayoutSettings settings,
        Vector2 viewportSize)
    {
        var placedStoryNodes = new List<ScenarioOptionNodeData>();
        var placedStorySizes = new List<Vector2>();

        PlaceStoryColumns(storyColumns, settings, placedStoryNodes, placedStorySizes);
        ResolveStoryNodeOverlaps(placedStoryNodes, placedStorySizes);
        ComputeStoryBounds(placedStoryNodes, placedStorySizes, out float maxStoryX, out float minAllY, out float maxAllY);

        float dividerX = maxStoryX + 185f;
        float dividerMinY = Mathf.Min(minAllY - 140f, -340f);
        float dividerMaxY = Mathf.Max(maxAllY + 140f, 340f);

        float maxAllX = PlaceEndingColumn(endingColumn, settings, dividerX, maxStoryX, ref minAllY, ref maxAllY);

        float viewportWidth = viewportSize.x > 0f ? viewportSize.x : 1920f;
        float viewportHeight = viewportSize.y > 0f ? viewportSize.y : 1012f;

        float totalWidth = Mathf.Max(viewportWidth, maxAllX + 340f);
        float maxHalfY = Mathf.Max(Mathf.Abs(minAllY), Mathf.Abs(maxAllY)) + 280f;
        float totalHeight = Mathf.Max(viewportHeight, maxHalfY * 2f);

        return new ScenarioGraphLayoutResult
        {
            dividerX = dividerX,
            dividerMinY = dividerMinY,
            dividerMaxY = dividerMaxY,
            contentSize = new Vector2(totalWidth, totalHeight)
        };
    }

    private static void PlaceStoryColumns(
        List<ScenarioColumnData> storyColumns,
        ScenarioGraphLayoutSettings settings,
        List<ScenarioOptionNodeData> placedNodes,
        List<Vector2> placedSizes)
    {
        if (storyColumns == null) return;

        float runningBranchY = 0f;

        for (int colIdx = 0; colIdx < storyColumns.Count; colIdx++)
        {
            ScenarioColumnData colData = storyColumns[colIdx];
            if (colData == null || colData.nodes == null || colData.nodes.Count == 0)
            {
                continue;
            }

            float baseX = LeftMarginX + (colIdx * settings.horizontalColumnSpacing);
            int count = colData.nodes.Count;

            if (colData.isRootEntryColumn)
            {
                ScenarioOptionNodeData rootNode = colData.nodes[0];
                if (!rootNode.hasCustomGraphPosition)
                {
                    rootNode.graphPosition = new Vector2(baseX, 0f);
                }
                placedNodes.Add(rootNode);
                placedSizes.Add(GetNodeBoxSize(rootNode, false, true, settings));
                continue;
            }

            ScenarioOptionNodeData currentInCol = FindCurrentRunNode(colData.nodes);
            bool onlyPreviousRunInCol = currentInCol == null;

            float waveY = ColumnWaveOffsetsY[colIdx % ColumnWaveOffsetsY.Length];
            float stageCenterY = Mathf.Clamp(runningBranchY + waveY * 0.45f, -320f, 320f);
            if (onlyPreviousRunInCol && colData.narrativeNodeId == SkippedLeakChoiceNodeId)
            {
                // Push skipped leaked-photo crisis visibly lower off the Safe branch
                stageCenterY = Mathf.Clamp(runningBranchY - 220f, -420f, 240f);
            }

            float spreadY = settings.verticalNodeSpacing * 1.12f;

            for (int rowIdx = 0; rowIdx < count; rowIdx++)
            {
                ScenarioOptionNodeData nodeData = colData.nodes[rowIdx];
                Vector2 boxSize = GetNodeBoxSize(nodeData, false, false, settings);

                if (!nodeData.hasCustomGraphPosition)
                {
                    float centeredIdx = ((count - 1) * 0.5f) - rowIdx;
                    int seed = (colData.narrativeNodeId * 31) + (rowIdx * 17) + colIdx;
                    float xJitter = ((seed % 5) - 2) * 18f;
                    float yJitter = (((seed * 7) % 5) - 2) * 16f;

                    nodeData.graphPosition = new Vector2(
                        baseX + xJitter,
                        stageCenterY + (centeredIdx * spreadY) + yJitter);
                }

                placedNodes.Add(nodeData);
                placedSizes.Add(boxSize);
            }

            if (currentInCol != null)
            {
                float drift = currentInCol.optionIndex == 0 ? 78f : (currentInCol.optionIndex == 2 ? -78f : 0f);
                runningBranchY = Mathf.Clamp(runningBranchY + drift, -280f, 280f);
            }
        }
    }

    private static ScenarioOptionNodeData FindCurrentRunNode(List<ScenarioOptionNodeData> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && nodes[i].visitState == ScenarioNodeVisitState.CurrentRun)
            {
                return nodes[i];
            }
        }
        return null;
    }

    private static void ResolveStoryNodeOverlaps(
        List<ScenarioOptionNodeData> placedNodes,
        List<Vector2> placedSizes)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            for (int a = 0; a < placedNodes.Count; a++)
            {
                for (int b = a + 1; b < placedNodes.Count; b++)
                {
                    Vector2 posA = placedNodes[a].graphPosition;
                    Vector2 posB = placedNodes[b].graphPosition;
                    float reqX = ((placedSizes[a].x + placedSizes[b].x) * 0.5f) + MinNodePadX;
                    float reqY = ((placedSizes[a].y + placedSizes[b].y) * 0.5f) + MinNodePadY;

                    float dx = Mathf.Abs(posB.x - posA.x);
                    float dy = Mathf.Abs(posB.y - posA.y);
                    if (dx >= reqX || dy >= reqY)
                    {
                        continue;
                    }

                    float pushY = (reqY - dy) * 0.55f;
                    float sign = posB.y >= posA.y ? 1f : -1f;

                    if (!placedNodes[a].hasCustomGraphPosition)
                    {
                        placedNodes[a].graphPosition = new Vector2(posA.x, posA.y - sign * pushY);
                    }
                    if (!placedNodes[b].hasCustomGraphPosition)
                    {
                        placedNodes[b].graphPosition = new Vector2(posB.x, posB.y + sign * pushY);
                    }
                }
            }
        }
    }

    private static void ComputeStoryBounds(
        List<ScenarioOptionNodeData> placedNodes,
        List<Vector2> placedSizes,
        out float maxStoryX,
        out float minAllY,
        out float maxAllY)
    {
        maxStoryX = LeftMarginX;
        minAllY = -120f;
        maxAllY = 120f;

        for (int i = 0; i < placedNodes.Count; i++)
        {
            Vector2 p = placedNodes[i].graphPosition;
            Vector2 s = placedSizes[i];
            maxStoryX = Mathf.Max(maxStoryX, p.x + s.x * 0.5f);
            minAllY = Mathf.Min(minAllY, p.y - s.y * 0.5f);
            maxAllY = Mathf.Max(maxAllY, p.y + s.y * 0.5f);
        }
    }

    private static float PlaceEndingColumn(
        ScenarioColumnData endingColumn,
        ScenarioGraphLayoutSettings settings,
        float dividerX,
        float initialMaxX,
        ref float minAllY,
        ref float maxAllY)
    {
        float maxAllX = initialMaxX;
        if (endingColumn == null || endingColumn.nodes == null || endingColumn.nodes.Count == 0)
        {
            return maxAllX;
        }

        float endingBaseX = dividerX + 210f;
        int eCount = endingColumn.nodes.Count;
        float endingSpacing = settings.verticalNodeSpacing + 45f;

        for (int e = 0; e < eCount; e++)
        {
            ScenarioOptionNodeData endNode = endingColumn.nodes[e];
            if (!endNode.hasCustomGraphPosition)
            {
                float centered = ((eCount - 1) * 0.5f) - e;
                endNode.graphPosition = new Vector2(endingBaseX, centered * endingSpacing);
            }

            Vector2 endSize = settings.endingNodeSize;
            maxAllX = Mathf.Max(maxAllX, endNode.graphPosition.x + endSize.x * 0.5f);
            minAllY = Mathf.Min(minAllY, endNode.graphPosition.y - endSize.y * 0.5f);
            maxAllY = Mathf.Max(maxAllY, endNode.graphPosition.y + endSize.y * 0.5f);
        }

        return maxAllX;
    }
}
