using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Main controller for the endgame ScenarioSystem UI Canvas.
/// Dynamically builds and renders the narrative decision tree / flowchart at the end of the campaign,
/// connecting taken decisions, displaying "???" with void-pointing arrows for untaken paths (Option B),
/// and converging into the 3 fixed story endings.
/// </summary>
public class ScenarioSystemUI : MonoBehaviour
{
    public static ScenarioSystemUI Instance { get; private set; }

    [Header("Branch Visibility Mode")]
    [Tooltip("Option B (Default/Preferred): Shows the first node of untaken branches as '???' with outgoing arrows pointing into the void, hiding exclusive child branches. Option A: Shows all nodes with '???'.")]
    [SerializeField] private ScenarioBranchMode _branchMode = ScenarioBranchMode.OptionB_FirstNodeWithVoidArrows;

    [Header("Per-Node Visibility & Annotation Overrides")]
    [Tooltip("Optional list of per-node overrides. Allows enabling/disabling whether specific NarrativeNodes appear in the ScenarioSystem summary screen.")]
    [SerializeField] private List<ScenarioNodeOverride> _nodeOverrides = new List<ScenarioNodeOverride>();

    [Header("Layout Settings")]
    [SerializeField] private Vector2 _decisionNodeSize = new Vector2(126f, 126f);
    [SerializeField] private Vector2 _untakenNodeSize = new Vector2(96f, 96f);
    [SerializeField] private Vector2 _endingNodeSize = new Vector2(142f, 132f);
    [SerializeField] private float _horizontalColumnSpacing = 250f;
    [SerializeField] private float _verticalNodeSpacing = 175f;
    [SerializeField] private float _voidArrowLength = 68f;

    [Header("Visual Palette (ScenarioSystemConcept)")]
    [SerializeField] private Color _backgroundColor = new Color(0.149f, 0.149f, 0.149f, 1f); // #262626
    [SerializeField] private Color _takenArrowColor = new Color(0.45f, 0.03f, 0.03f, 1f);    // #730808
    [SerializeField] private Color _untakenArrowColor = new Color(0.40f, 0.02f, 0.02f, 0.9f); // #660505

    [Header("Custom Visual Sprites (Optional — falls back to procedural generation if null)")]
    [Tooltip("Optional custom sprite for nodes representing decisions taken. If unassigned, procedural rounded rect is used.")]
    [SerializeField] private Sprite _customTakenNodeSprite;

    [Tooltip("Optional custom sprite for untaken nodes ('???'). If unassigned, procedural rounded rect is used.")]
    [SerializeField] private Sprite _customUntakenNodeSprite;

    [Tooltip("Optional custom sprite for the 3 ending nodes. If unassigned, procedural rounded rect or _customTakenNodeSprite is used.")]
    [SerializeField] private Sprite _customEndingNodeSprite;

    [Tooltip("Optional custom sprite for the node when hovered by mouse.")]
    [SerializeField] private Sprite _customHoverNodeSprite;

    [Tooltip("Optional custom sprite for the hover annotation tooltip box. If unassigned, procedural rounded rect is used.")]
    [SerializeField] private Sprite _customTooltipSprite;

    [Tooltip("Optional custom sprite for the connection arrowhead. If unassigned, procedural triangle arrow is used.")]
    [SerializeField] private Sprite _customArrowHeadSprite;

    [Tooltip("Optional custom sprite for connection arrow shafts. If unassigned, a solid color quad is used.")]
    [SerializeField] private Sprite _customArrowShaftSprite;

    [Tooltip("Optional custom sprite for header bar buttons. If unassigned, procedural rounded rect is used.")]
    [SerializeField] private Sprite _customHeaderButtonSprite;

    [Tooltip("Optional custom background sprite for the entire ScenarioSystem canvas/panel.")]
    [SerializeField] private Sprite _customBackgroundSprite;

    [Tooltip("If true, retains the grey/dark theme tints on custom node sprites. If false, custom sprites show their original colors.")]
    [SerializeField] private bool _tintCustomNodeSprites = false;

    [Header("UI References (Auto-Built if Unassigned)")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _viewportRect;
    [SerializeField] private RectTransform _graphContentRect;
    [SerializeField] private RectTransform _connectionsLayerRect;
    [SerializeField] private RectTransform _nodesLayerRect;
    [SerializeField] private ScenarioAnnotationTooltipUI _tooltipUI;
    [SerializeField] private TextMeshProUGUI _headerSummaryText;
    [SerializeField] private TextMeshProUGUI _modeButtonLabel;
    [SerializeField] private Button _toggleModeButton;
    [SerializeField] private Button _resetViewButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _closeButton;

    private readonly List<GameObject> _spawnedGraphObjects = new List<GameObject>();
    private ScenarioNodeUI _currentlyHoveredNode;
    private bool _isHierarchyInitialized;
    private float _previousTimeScale = 1f;
    private bool _hasCapturedTimeScale;

    public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;
    public ScenarioBranchMode BranchMode => _branchMode;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureHierarchyBuilt();

        // Guarantee the ScenarioSystem UI is deactivated by default during gameplay
        HideScenarioTree();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Activates the ScenarioSystem Canvas and populates the decision tree from the current campaign state.
    /// Called exclusively during the Ending Sequence (or via debug test controls).
    /// </summary>
    private void Update()
    {
        if (!IsVisible) return;

        // Keep mouse cursor visible and unlocked while exploring the scenario flowchart
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }

        // Allow closing the ScenarioSystem with Escape
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                HideScenarioTree();
            }
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        EnforceFullScreenLayout();
        if (IsVisible)
        {
            AdjustContentBoundsToViewport();
        }
    }

    /// <summary>
    /// Activates the ScenarioSystem Canvas and populates the decision tree from the current campaign state.
    /// Freezes gameplay simulation and disables player movement/interactions while active.
    /// </summary>
    public void ShowScenarioTree()
    {
        EnsureHierarchyBuilt();
        EnforceFullScreenLayout();

        // 1. Freeze gameplay simulation while viewing the scenario tree
        if (!_hasCapturedTimeScale)
        {
            _previousTimeScale = Time.timeScale;
            _hasCapturedTimeScale = true;
        }
        Time.timeScale = 0f;

        // 2. Completely lock player movement, look, interact, and phone inputs
        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteract();
            InputManager.Instance.DisablePhone();
            InputManager.Instance.DisableMenuAction();
        }

        PlayerController player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            player.StopMovementImmediately();
        }

        // 3. Unlock and display mouse cursor for flowchart navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UnlockMouse();
        }

        if (_canvas != null)
        {
            _canvas.enabled = true;
            _canvas.sortingOrder = 500;
        }

        if (_panelRoot != null)
        {
            _panelRoot.SetActive(true);
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        RebuildScenarioTree();

        if (isActiveAndEnabled)
        {
            StartCoroutine(ScrollToStartNextFrame());
        }
    }

    /// <summary>
    /// Hides the ScenarioSystem summary screen and any active hover annotation tooltip,
    /// restoring gameplay timeScale and player inputs according to the active UI state.
    /// </summary>
    public void HideScenarioTree()
    {
        _currentlyHoveredNode = null;
        if (_tooltipUI != null)
        {
            _tooltipUI.HideImmediate();
        }

        if (_panelRoot != null)
        {
            _panelRoot.SetActive(false);
        }

        // 1. Restore simulation timeScale
        if (_hasCapturedTimeScale)
        {
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            _hasCapturedTimeScale = false;
        }
        else
        {
            Time.timeScale = 1f;
        }

        // 2. Restore player gameplay inputs
        RestoreGameplayInputs();
    }

    /// <summary>
    /// Restores player movement and interaction maps based on the current UIManager UIState.
    /// </summary>
    private void RestoreGameplayInputs()
    {
        if (UIManager.Instance == null)
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnableMovementMap();
                InputManager.Instance.EnablePhone();
                InputManager.Instance.EnableMenuAction();
                InputManager.Instance.EnableInteract();
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        UIState state = UIManager.Instance.GetCurrentState();
        switch (state)
        {
            case UIState.DEFAULT:
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.EnableMovementMap();
                    InputManager.Instance.EnablePhone();
                    InputManager.Instance.EnableMenuAction();
                    InputManager.Instance.EnableInteract();
                }
                UIManager.Instance.LockMouse();
                break;

            case UIState.PHONE:
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisableMenuAction();
                    InputManager.Instance.EnablePhone();
                }
                UIManager.Instance.UnlockMouse();
                break;

            case UIState.MAP:
            case UIState.CONVERSATION:
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                    InputManager.Instance.EnableMenuAction();
                }
                UIManager.Instance.UnlockMouse();
                break;

            case UIState.MENU:
                Time.timeScale = 0f;
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                    InputManager.Instance.EnableMenuAction();
                }
                UIManager.Instance.UnlockMouse();
                break;

            case UIState.MAIN_MENU:
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                    InputManager.Instance.DisableMenuAction();
                }
                UIManager.Instance.UnlockMouse();
                break;

            default:
                UIManager.Instance.UnlockMouse();
                break;
        }
    }

    /// <summary>
    /// Allows configuring at runtime whether a specific NarrativeNode ID is included in the ScenarioSystem tree.
    /// </summary>
    public void SetNodeShowInScenarioTree(int narrativeNodeId, bool show)
    {
        for (int i = 0; i < _nodeOverrides.Count; i++)
        {
            if (_nodeOverrides[i] != null && _nodeOverrides[i].narrativeNodeId == narrativeNodeId)
            {
                _nodeOverrides[i].showInScenarioTree = show;
                if (IsVisible) RebuildScenarioTree();
                return;
            }
        }

        _nodeOverrides.Add(new ScenarioNodeOverride
        {
            narrativeNodeId = narrativeNodeId,
            showInScenarioTree = show
        });

        if (MessagesManager.Instance != null)
        {
            NarrativeNode node = MessagesManager.Instance.GetNarrativeNode(narrativeNodeId);
            node?.SetShowInScenarioTree(show);
        }

        if (IsVisible)
        {
            RebuildScenarioTree();
        }
    }

    /// <summary>
    /// Switches between Option B (First untaken node with void arrows & hidden exclusive child branches)
    /// and Option A (All untaken nodes shown as "???") and refreshes the flowchart.
    /// </summary>
    public void ToggleBranchMode()
    {
        _branchMode = _branchMode == ScenarioBranchMode.OptionB_FirstNodeWithVoidArrows
            ? ScenarioBranchMode.OptionA_ShowAllNodesAsHidden
            : ScenarioBranchMode.OptionB_FirstNodeWithVoidArrows;

        UpdateModeButtonLabel();
        RebuildScenarioTree();
    }

    /// <summary>
    /// Re-collects all decision data from DayManager, ReputationManager, DecisionDataManager, and MessagesManager
    /// and reconstructs all nodes and directed connection arrows.
    /// </summary>
    public void RebuildScenarioTree()
    {
        EnsureHierarchyBuilt();
        ClearSpawnedGraphObjects();

        if (_tooltipUI != null)
        {
            _tooltipUI.HideImmediate();
        }

        ScenarioTreeModel model = ScenarioDataCollector.CollectTreeModel(_branchMode, _nodeOverrides);
        UpdateHeaderSummary(model);
        UpdateModeButtonLabel();

        // Combine columns in left-to-right narrative sequence: [Root] -> [Decision Columns...] -> [3 Fixed Endings]
        List<ScenarioColumnData> allColumns = new List<ScenarioColumnData>();
        if (model.rootColumn != null)
        {
            allColumns.Add(model.rootColumn);
        }
        if (model.decisionColumns != null)
        {
            allColumns.AddRange(model.decisionColumns);
        }
        if (model.endingColumn != null)
        {
            allColumns.Add(model.endingColumn);
        }

        if (allColumns.Count == 0)
        {
            CreateEmptyStateLabel("No narrative decisions have been taken yet.");
            return;
        }

        const float leftMargin = 160f;
        const float rightMargin = 220f;
        float totalWidth = leftMargin + Mathf.Max(1, allColumns.Count - 1) * _horizontalColumnSpacing + rightMargin;
        float viewportWidth = (_viewportRect != null && _viewportRect.rect.width > 0f) ? _viewportRect.rect.width : 1920f;
        float viewportHeight = (_viewportRect != null && _viewportRect.rect.height > 0f) ? _viewportRect.rect.height : 1012f;

        float totalContentWidth = Mathf.Max(viewportWidth, totalWidth);
        float totalHeight = Mathf.Max(viewportHeight, 920f);

        if (_graphContentRect != null)
        {
            _graphContentRect.sizeDelta = new Vector2(totalContentWidth, totalHeight);
        }

        List<List<ScenarioNodeUI>> spawnedColumns = new List<List<ScenarioNodeUI>>(allColumns.Count);

        // 1. Instantiate all visible columns and their nodes
        for (int colIdx = 0; colIdx < allColumns.Count; colIdx++)
        {
            ScenarioColumnData colData = allColumns[colIdx];
            float columnX = leftMargin + (colIdx * _horizontalColumnSpacing);

            CreateColumnHeaderLabel(colData, columnX, totalHeight * 0.5f - 58f);

            List<ScenarioNodeUI> columnNodeUIs = new List<ScenarioNodeUI>();
            int count = colData.nodes.Count;

            for (int rowIdx = 0; rowIdx < count; rowIdx++)
            {
                ScenarioOptionNodeData nodeData = colData.nodes[rowIdx];

                // Center the column vertically around Y = 0
                float centeredOffsetIndex = ( (count - 1) * 0.5f ) - rowIdx;
                float spacing = colData.isEndingColumn ? (_verticalNodeSpacing + 28f) : _verticalNodeSpacing;
                float nodeY = centeredOffsetIndex * spacing;

                // Slight organic vertical stagger for untaken nodes to mirror ScenarioSystemConcept.png
                if (!nodeData.isTaken && !colData.isEndingColumn && !colData.isRootEntryColumn)
                {
                    nodeY += (rowIdx == 0 ? 10f : (rowIdx == 2 ? -10f : 0f));
                }

                Vector2 size = _decisionNodeSize;
                if (colData.isEndingColumn)
                {
                    size = _endingNodeSize;
                }
                else if (!nodeData.isTaken && !colData.isRootEntryColumn)
                {
                    size = _untakenNodeSize;
                }

                ScenarioNodeUI nodeUI = CreateNodeUI(nodeData, new Vector2(columnX, nodeY), size);
                columnNodeUIs.Add(nodeUI);
            }

            spawnedColumns.Add(columnNodeUIs);
        }

        // Force layout/transform update before computing world-space connection coordinates
        Canvas.ForceUpdateCanvases();

        // 2. Draw directed dark-red arrows between columns and outgoing void arrows for untaken nodes
        DrawTreeConnections(allColumns, spawnedColumns);
    }

    private void UpdateHeaderSummary(ScenarioTreeModel model)
    {
        if (_headerSummaryText == null || model == null)
        {
            return;
        }

        if (model.isCampaignCompleted)
        {
            _headerSummaryText.text =
                $"SCENARIO SYSTEM  |  Ending Reached: <color=#D8DF64>{model.reachedEndingTitle}</color>  |  " +
                $"Final Safety / Reputation: <color=#7CE08A>{model.finalReputation}/100</color>  |  " +
                $"Hover over any node to read its annotation";
        }
        else
        {
            int currentDay = DayManager.Instance != null ? DayManager.Instance.GetCurrentDay() : 1;
            int decisionsTaken = model.decisionColumns != null ? model.decisionColumns.Count : 0;
            if (model.rootColumn == null && decisionsTaken == 0)
            {
                _headerSummaryText.text =
                    $"SCENARIO SYSTEM (PREVIEW)  |  Day {currentDay}  |  " +
                    $"<color=#C8C8C8>No narrative decisions have been taken yet.</color>";
            }
            else
            {
                _headerSummaryText.text =
                    $"SCENARIO SYSTEM (PREVIEW)  |  Campaign in Progress: <color=#D8DF64>Day {currentDay}</color>  |  " +
                    $"Decisions Taken: <color=#D8DF64>{decisionsTaken}</color>  |  " +
                    $"Current Safety / Reputation: <color=#7CE08A>{model.finalReputation}/100</color>";
            }
        }
    }

    private void CreateEmptyStateLabel(string message)
    {
        if (_nodesLayerRect == null) return;

        GameObject emptyObj = new GameObject("EmptyStateMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
        emptyObj.transform.SetParent(_nodesLayerRect, false);
        _spawnedGraphObjects.Add(emptyObj);

        RectTransform rect = emptyObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(960f, 0f);
        rect.sizeDelta = new Vector2(700f, 140f);

        TextMeshProUGUI tmp = emptyObj.GetComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = 20f;
        tmp.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private void UpdateModeButtonLabel()
    {
        if (_modeButtonLabel == null)
        {
            return;
        }

        _modeButtonLabel.text = _branchMode == ScenarioBranchMode.OptionB_FirstNodeWithVoidArrows
            ? "Mode: Option B (Void Arrows)"
            : "Mode: Option A (All Nodes)";
    }

    private void DrawTreeConnections(
        List<ScenarioColumnData> allColumns,
        List<List<ScenarioNodeUI>> spawnedColumns)
    {
        if (_connectionsLayerRect == null)
        {
            return;
        }

        for (int colIdx = 0; colIdx < spawnedColumns.Count; colIdx++)
        {
            List<ScenarioNodeUI> currentColNodes = spawnedColumns[colIdx];
            ScenarioColumnData currentColData = allColumns[colIdx];

            // Find the taken node(s) in the current column that will propagate to the next column
            ScenarioNodeUI takenNodeInCurrentCol = null;
            for (int i = 0; i < currentColNodes.Count; i++)
            {
                if (currentColNodes[i] != null && currentColNodes[i].NodeData != null && currentColNodes[i].NodeData.isTaken)
                {
                    takenNodeInCurrentCol = currentColNodes[i];
                    break;
                }
            }

            // Connect the taken node in this column to every option node in the next column (including the 3 Fixed Endings!)
            if (colIdx + 1 < spawnedColumns.Count && takenNodeInCurrentCol != null)
            {
                List<ScenarioNodeUI> nextColNodes = spawnedColumns[colIdx + 1];
                int nextCount = nextColNodes.Count;

                for (int nextIdx = 0; nextIdx < nextCount; nextIdx++)
                {
                    ScenarioNodeUI targetNode = nextColNodes[nextIdx];
                    if (targetNode == null) continue;

                    float normalizedFan = nextCount > 1
                        ? Mathf.Lerp(0.85f, -0.85f, (float)nextIdx / (nextCount - 1))
                        : 0f;

                    Vector3 startWorld = takenNodeInCurrentCol.GetRightConnectionWorldPoint(normalizedFan);
                    Vector3 endWorld = targetNode.GetLeftConnectionWorldPoint(0f);

                    Vector2 startLocal = _connectionsLayerRect.InverseTransformPoint(startWorld);
                    Vector2 endLocal = _connectionsLayerRect.InverseTransformPoint(endWorld);

                    bool leadsToTaken = targetNode.NodeData != null && targetNode.NodeData.isTaken;
                    Color arrowColor = leadsToTaken ? _takenArrowColor : _untakenArrowColor;
                    float thickness = leadsToTaken ? 3.2f : 2.4f;

                    CreateDirectedArrow(startLocal, endLocal, arrowColor, thickness);
                }
            }

            // Option B: For untaken decision nodes ("???"), draw outgoing arrows pointing into the void ("hacia la nada")
            if (!currentColData.isEndingColumn && !currentColData.isRootEntryColumn)
            {
                for (int i = 0; i < currentColNodes.Count; i++)
                {
                    ScenarioNodeUI nodeUI = currentColNodes[i];
                    if (nodeUI == null || nodeUI.NodeData == null || nodeUI.NodeData.isTaken)
                    {
                        continue;
                    }

                    if (nodeUI.NodeData.hasOutgoingVoidArrows && nodeUI.NodeData.voidArrowCount > 0)
                    {
                        DrawOutgoingVoidArrows(nodeUI, nodeUI.NodeData.voidArrowCount);
                    }
                    else if (_branchMode == ScenarioBranchMode.OptionA_ShowAllNodesAsHidden && colIdx + 1 < spawnedColumns.Count)
                    {
                        // Under Option A, if a whole column is a skipped branch, still connect it visually so it isn't floating
                        DrawOutgoingVoidArrows(nodeUI, 2);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draws short diverging dark-red arrows from an untaken "???" node pointing into empty space (the void),
    /// matching ScenarioSystemConcept.png and Option B requirements.
    /// </summary>
    private void DrawOutgoingVoidArrows(ScenarioNodeUI untakenNode, int arrowCount)
    {
        if (untakenNode == null || _connectionsLayerRect == null)
        {
            return;
        }

        for (int a = 0; a < arrowCount; a++)
        {
            float fan = arrowCount > 1 ? Mathf.Lerp(0.55f, -0.55f, (float)a / (arrowCount - 1)) : 0f;
            Vector3 startWorld = untakenNode.GetRightConnectionWorldPoint(fan);
            Vector2 startLocal = _connectionsLayerRect.InverseTransformPoint(startWorld);

            float angleDeg = fan * 24f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            Vector2 endLocal = startLocal + (direction * _voidArrowLength);

            CreateDirectedArrow(startLocal, endLocal, _untakenArrowColor, 2.3f);
        }
    }

    /// <summary>
    /// Creates a UI line segment with a triangular arrowhead pointing from startPos to endPos inside _connectionsLayerRect.
    /// </summary>
    private void CreateDirectedArrow(Vector2 startPos, Vector2 endPos, Color color, float thickness)
    {
        Vector2 delta = endPos - startPos;
        float fullDistance = delta.magnitude;
        if (fullDistance < 8f)
        {
            return;
        }

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        const float arrowHeadLength = 13f;
        const float arrowHeadWidth = 12f;

        // Shorten shaft slightly so it meets the base of the triangular arrowhead cleanly
        float shaftLength = Mathf.Max(2f, fullDistance - (arrowHeadLength * 0.75f));
        Vector2 dir = delta / fullDistance;
        Vector2 shaftMidpoint = startPos + dir * (shaftLength * 0.5f);

        // 1. Line shaft
        GameObject shaftObj = new GameObject("ArrowShaft", typeof(RectTransform), typeof(Image));
        shaftObj.transform.SetParent(_connectionsLayerRect, false);
        _spawnedGraphObjects.Add(shaftObj);

        RectTransform shaftRect = shaftObj.GetComponent<RectTransform>();
        shaftRect.anchorMin = new Vector2(0.5f, 0.5f);
        shaftRect.anchorMax = new Vector2(0.5f, 0.5f);
        shaftRect.pivot = new Vector2(0.5f, 0.5f);
        shaftRect.anchoredPosition = shaftMidpoint;
        shaftRect.sizeDelta = new Vector2(shaftLength, thickness);
        shaftRect.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image shaftImg = shaftObj.GetComponent<Image>();
        if (_customArrowShaftSprite != null)
        {
            shaftImg.sprite = _customArrowShaftSprite;
            shaftImg.type = (_customArrowShaftSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
        }
        shaftImg.color = color;
        shaftImg.raycastTarget = false;

        // 2. Triangular arrowhead
        GameObject headObj = new GameObject("ArrowHead", typeof(RectTransform), typeof(Image));
        headObj.transform.SetParent(_connectionsLayerRect, false);
        _spawnedGraphObjects.Add(headObj);

        RectTransform headRect = headObj.GetComponent<RectTransform>();
        headRect.anchorMin = new Vector2(0.5f, 0.5f);
        headRect.anchorMax = new Vector2(0.5f, 0.5f);
        headRect.pivot = new Vector2(0.5f, 0.5f);
        headRect.anchoredPosition = endPos - dir * (arrowHeadLength * 0.45f);
        headRect.sizeDelta = new Vector2(arrowHeadLength, arrowHeadWidth);
        headRect.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image headImg = headObj.GetComponent<Image>();
        headImg.sprite = _customArrowHeadSprite != null ? _customArrowHeadSprite : ScenarioProceduralSprites.GetTriangleArrowSprite();
        headImg.color = color;
        headImg.raycastTarget = false;
    }

    private void CreateColumnHeaderLabel(ScenarioColumnData colData, float columnX, float headerY)
    {
        if (_nodesLayerRect == null || colData == null)
        {
            return;
        }

        GameObject headerObj = new GameObject($"ColumnHeader_{colData.columnIndex}", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObj.transform.SetParent(_nodesLayerRect, false);
        _spawnedGraphObjects.Add(headerObj);

        RectTransform rect = headerObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(columnX, headerY);
        rect.sizeDelta = new Vector2(210f, 44f);

        TextMeshProUGUI tmp = headerObj.GetComponent<TextMeshProUGUI>();
        tmp.text = colData.columnHeader;
        tmp.fontSize = 13f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = colData.isEndingColumn
            ? new Color(0.86f, 0.89f, 0.42f, 0.95f)
            : new Color(0.75f, 0.75f, 0.75f, 0.78f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private ScenarioNodeUI CreateNodeUI(ScenarioOptionNodeData nodeData, Vector2 anchoredPos, Vector2 size)
    {
        GameObject nodeObj = new GameObject(
            $"ScenarioNode_{nodeData.parentNodeId}_Opt{nodeData.optionIndex}",
            typeof(RectTransform),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(ScenarioNodeUI));

        nodeObj.transform.SetParent(_nodesLayerRect, false);
        _spawnedGraphObjects.Add(nodeObj);

        RectTransform nodeRect = nodeObj.GetComponent<RectTransform>();
        nodeRect.anchorMin = new Vector2(0f, 0.5f);
        nodeRect.anchorMax = new Vector2(0f, 0.5f);
        nodeRect.pivot = new Vector2(0.5f, 0.5f);
        nodeRect.anchoredPosition = anchoredPos;
        nodeRect.sizeDelta = size;

        Image bgImage = nodeObj.GetComponent<Image>();
        UnityEngine.UI.Outline outline = nodeObj.GetComponent<UnityEngine.UI.Outline>();

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(nodeObj.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 10f);
        labelRect.offsetMax = new Vector2(-12f, -10f);

        TextMeshProUGUI labelTmp = labelObj.GetComponent<TextMeshProUGUI>();
        labelTmp.textWrappingMode = TextWrappingModes.Normal;
        labelTmp.overflowMode = TextOverflowModes.Ellipsis;

        Sprite fallbackSprite = nodeData.isEndingNode
            ? (_customEndingNodeSprite != null ? _customEndingNodeSprite : _customTakenNodeSprite)
            : (nodeData.isTaken ? _customTakenNodeSprite : _customUntakenNodeSprite);

        ScenarioNodeUI nodeUI = nodeObj.GetComponent<ScenarioNodeUI>();
        nodeUI.ConfigureReferences(nodeRect, bgImage, outline, labelTmp);
        nodeUI.Initialize(
            nodeData,
            OnNodeHoverEnter,
            OnNodeHoverExit,
            fallbackSprite,
            _customHoverNodeSprite,
            _tintCustomNodeSprites);

        return nodeUI;
    }

    private void OnNodeHoverEnter(ScenarioNodeUI hoveredNode)
    {
        _currentlyHoveredNode = hoveredNode;
        if (_tooltipUI != null && hoveredNode != null)
        {
            RectTransform tooltipContainer = _viewportRect != null ? _viewportRect : _panelRoot.GetComponent<RectTransform>();
            _tooltipUI.ShowForNode(hoveredNode, tooltipContainer);
        }
    }

    private void OnNodeHoverExit(ScenarioNodeUI exitedNode)
    {
        if (_currentlyHoveredNode == exitedNode)
        {
            _currentlyHoveredNode = null;
            if (_tooltipUI != null)
            {
                _tooltipUI.HideImmediate();
            }
        }
    }

    private void ClearSpawnedGraphObjects()
    {
        for (int i = 0; i < _spawnedGraphObjects.Count; i++)
        {
            if (_spawnedGraphObjects[i] != null)
            {
                Destroy(_spawnedGraphObjects[i]);
            }
        }
        _spawnedGraphObjects.Clear();
    }

    private IEnumerator ScrollToStartNextFrame()
    {
        yield return null;
        if (_scrollRect != null)
        {
            _scrollRect.horizontalNormalizedPosition = 0f;
            _scrollRect.verticalNormalizedPosition = 0.5f;
        }
    }

    private void OnResetViewClicked()
    {
        if (_scrollRect != null)
        {
            _scrollRect.horizontalNormalizedPosition = 0f;
            _scrollRect.verticalNormalizedPosition = 0.5f;
        }
    }

    private void OnMainMenuClicked()
    {
        HideScenarioTree();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeScene("MainMenu");
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    /// <summary>
    /// Ensures the standalone Canvas and all child UI layers exist and are properly wired.
    /// Safe to call both at runtime and from the Editor prefab builder.
    /// </summary>
    /// <summary>
    /// Enforces that the canvas and panel stretch across 100% of the screen regardless of resolution or aspect ratio.
    /// </summary>
    public void EnforceFullScreenLayout()
    {
        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        }

        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (_panelRoot != null)
        {
            RectTransform panelRect = _panelRoot.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                panelRect.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        if (_viewportRect != null)
        {
            _viewportRect.anchorMin = Vector2.zero;
            _viewportRect.anchorMax = Vector2.one;
            _viewportRect.offsetMin = Vector2.zero;
            _viewportRect.offsetMax = new Vector2(0f, -68f);
        }

        if (_connectionsLayerRect != null)
        {
            _connectionsLayerRect.anchorMin = Vector2.zero;
            _connectionsLayerRect.anchorMax = Vector2.one;
            _connectionsLayerRect.offsetMin = Vector2.zero;
            _connectionsLayerRect.offsetMax = Vector2.zero;
        }

        if (_nodesLayerRect != null)
        {
            _nodesLayerRect.anchorMin = Vector2.zero;
            _nodesLayerRect.anchorMax = Vector2.one;
            _nodesLayerRect.offsetMin = Vector2.zero;
            _nodesLayerRect.offsetMax = Vector2.zero;
        }
    }

    private void AdjustContentBoundsToViewport()
    {
        if (_graphContentRect == null || _viewportRect == null) return;
        float viewportWidth = _viewportRect.rect.width > 0f ? _viewportRect.rect.width : 1920f;
        float viewportHeight = _viewportRect.rect.height > 0f ? _viewportRect.rect.height : 1012f;

        Vector2 currentSize = _graphContentRect.sizeDelta;
        _graphContentRect.sizeDelta = new Vector2(
            Mathf.Max(viewportWidth, currentSize.x),
            Mathf.Max(viewportHeight, currentSize.y));
    }

    public void EnsureHierarchyBuilt()
    {
        if (_isHierarchyInitialized && _panelRoot != null && _graphContentRect != null && _tooltipUI != null)
        {
            EnforceFullScreenLayout();
            BindButtonListeners();
            return;
        }

        // 1. Standalone Canvas configuration
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvas = gameObject.AddComponent<Canvas>();
        }
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // 2. If child hierarchy already exists (e.g., instantiated from ScenarioSystem.prefab), bind and return
        if (_panelRoot != null && _graphContentRect != null && _connectionsLayerRect != null && _nodesLayerRect != null && _tooltipUI != null)
        {
            _isHierarchyInitialized = true;
            if (_customBackgroundSprite != null)
            {
                Image bg = _panelRoot.GetComponent<Image>();
                if (bg != null)
                {
                    bg.sprite = _customBackgroundSprite;
                    bg.type = (_customBackgroundSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                    bg.color = Color.white;
                }
            }
            if (_customTooltipSprite != null && _tooltipUI != null)
            {
                _tooltipUI.SetCustomSprite(_customTooltipSprite);
            }
            BindButtonListeners();
            return;
        }

        // 3. Build clean, self-contained ScenarioSystem Canvas hierarchy matching ScenarioSystemConcept.png
        GameObject panelObj = new GameObject("ScenarioSystemPanel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(transform, false);
        _panelRoot = panelObj;

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = panelObj.GetComponent<Image>();
        if (_customBackgroundSprite != null)
        {
            panelBg.sprite = _customBackgroundSprite;
            panelBg.type = (_customBackgroundSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            panelBg.color = Color.white;
        }
        else
        {
            panelBg.color = _backgroundColor;
        }
        panelBg.raycastTarget = true;

        // Top Header Bar
        GameObject headerObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
        headerObj.transform.SetParent(panelObj.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 68f);

        Image headerBg = headerObj.GetComponent<Image>();
        headerBg.color = new Color(0.11f, 0.11f, 0.11f, 0.96f);

        // Header Summary Text
        GameObject summaryTextObj = new GameObject("HeaderSummaryText", typeof(RectTransform), typeof(TextMeshProUGUI));
        summaryTextObj.transform.SetParent(headerObj.transform, false);
        RectTransform summaryRect = summaryTextObj.GetComponent<RectTransform>();
        summaryRect.anchorMin = new Vector2(0f, 0f);
        summaryRect.anchorMax = new Vector2(0.62f, 1f);
        summaryRect.offsetMin = new Vector2(28f, 6f);
        summaryRect.offsetMax = new Vector2(-10f, -6f);

        _headerSummaryText = summaryTextObj.GetComponent<TextMeshProUGUI>();
        _headerSummaryText.fontSize = 18f;
        _headerSummaryText.color = Color.white;
        _headerSummaryText.alignment = TextAlignmentOptions.MidlineLeft;
        _headerSummaryText.text = "SCENARIO SYSTEM — NARRATIVE PATH SUMMARY";

        // Header Action Buttons (Mode Toggle, Reset View, Main Menu, Close)
        _toggleModeButton = CreateHeaderButton(
            headerObj.transform,
            "ToggleModeButton",
            new Vector2(-460f, 0f),
            new Vector2(210f, 42f),
            "Mode: Option B (Void Arrows)",
            new Color(0.28f, 0.28f, 0.28f, 1f),
            out _modeButtonLabel);

        _resetViewButton = CreateHeaderButton(
            headerObj.transform,
            "ResetViewButton",
            new Vector2(-285f, 0f),
            new Vector2(120f, 42f),
            "Reset View",
            new Color(0.28f, 0.28f, 0.28f, 1f),
            out _);

        _mainMenuButton = CreateHeaderButton(
            headerObj.transform,
            "MainMenuButton",
            new Vector2(-145f, 0f),
            new Vector2(135f, 42f),
            "Main Menu",
            new Color(0.45f, 0.08f, 0.08f, 1f),
            out _);

        _closeButton = CreateHeaderButton(
            headerObj.transform,
            "CloseButton",
            new Vector2(-42f, 0f),
            new Vector2(52f, 42f),
            "X",
            new Color(0.25f, 0.25f, 0.25f, 1f),
            out _);

        // Scrollable Flowchart Viewport
        GameObject scrollObj = new GameObject("FlowchartScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollObj.transform.SetParent(panelObj.transform, false);
        _viewportRect = scrollObj.GetComponent<RectTransform>();
        _viewportRect.anchorMin = Vector2.zero;
        _viewportRect.anchorMax = Vector2.one;
        _viewportRect.offsetMin = new Vector2(0f, 0f);
        _viewportRect.offsetMax = new Vector2(0f, -68f);

        Image viewportImg = scrollObj.GetComponent<Image>();
        viewportImg.color = new Color(0.149f, 0.149f, 0.149f, 1f);

        Mask viewportMask = scrollObj.GetComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        // Graph Content Container inside ScrollView
        GameObject contentObj = new GameObject("GraphContent", typeof(RectTransform));
        contentObj.transform.SetParent(scrollObj.transform, false);
        _graphContentRect = contentObj.GetComponent<RectTransform>();
        _graphContentRect.anchorMin = new Vector2(0f, 0.5f);
        _graphContentRect.anchorMax = new Vector2(0f, 0.5f);
        _graphContentRect.pivot = new Vector2(0f, 0.5f);
        _graphContentRect.anchoredPosition = Vector2.zero;
        _graphContentRect.sizeDelta = new Vector2(3600f, 920f);

        // Connections Layer (Rendered behind nodes)
        GameObject connectionsObj = new GameObject("ConnectionsLayer", typeof(RectTransform));
        connectionsObj.transform.SetParent(_graphContentRect, false);
        _connectionsLayerRect = connectionsObj.GetComponent<RectTransform>();
        _connectionsLayerRect.anchorMin = Vector2.zero;
        _connectionsLayerRect.anchorMax = Vector2.one;
        _connectionsLayerRect.offsetMin = Vector2.zero;
        _connectionsLayerRect.offsetMax = Vector2.zero;

        // Nodes Layer (Rendered in front of connections)
        GameObject nodesObj = new GameObject("NodesLayer", typeof(RectTransform));
        nodesObj.transform.SetParent(_graphContentRect, false);
        _nodesLayerRect = nodesObj.GetComponent<RectTransform>();
        _nodesLayerRect.anchorMin = Vector2.zero;
        _nodesLayerRect.anchorMax = Vector2.one;
        _nodesLayerRect.offsetMin = Vector2.zero;
        _nodesLayerRect.offsetMax = Vector2.zero;

        _scrollRect = scrollObj.GetComponent<ScrollRect>();
        _scrollRect.viewport = _viewportRect;
        _scrollRect.content = _graphContentRect;
        _scrollRect.horizontal = true;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 35f;
        _scrollRect.inertia = true;
        _scrollRect.decelerationRate = 0.12f;

        // Olive-Green Hover Annotation Tooltip Box (matches ScenarioSystemConcept.png)
        GameObject tooltipObj = new GameObject(
            "ScenarioAnnotationTooltip",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(ScenarioAnnotationTooltipUI));
        tooltipObj.transform.SetParent(_viewportRect, false);

        RectTransform tooltipRect = tooltipObj.GetComponent<RectTransform>();
        tooltipRect.sizeDelta = new Vector2(235f, 185f);

        Image tooltipBg = tooltipObj.GetComponent<Image>();
        CanvasGroup tooltipCg = tooltipObj.GetComponent<CanvasGroup>();

        GameObject tooltipTextObj = new GameObject("AnnotationText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tooltipTextObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform tooltipTextRect = tooltipTextObj.GetComponent<RectTransform>();
        tooltipTextRect.anchorMin = Vector2.zero;
        tooltipTextRect.anchorMax = Vector2.one;
        tooltipTextRect.offsetMin = new Vector2(16f, 14f);
        tooltipTextRect.offsetMax = new Vector2(-16f, -14f);

        TextMeshProUGUI tooltipTmp = tooltipTextObj.GetComponent<TextMeshProUGUI>();
        tooltipTmp.fontSize = 15f;
        tooltipTmp.color = Color.white;
        tooltipTmp.alignment = TextAlignmentOptions.TopLeft;

        _tooltipUI = tooltipObj.GetComponent<ScenarioAnnotationTooltipUI>();
        _tooltipUI.ConfigureReferences(tooltipRect, tooltipBg, tooltipTmp, tooltipCg);
        if (_customTooltipSprite != null)
        {
            _tooltipUI.SetCustomSprite(_customTooltipSprite);
        }
        _tooltipUI.HideImmediate();

        _isHierarchyInitialized = true;
        BindButtonListeners();
    }

    /// <summary>
    /// Configures custom sprites for nodes at runtime, with fallback to procedural graphics if null.
    /// </summary>
    public void SetCustomNodeSprites(Sprite taken, Sprite untaken = null, Sprite ending = null, Sprite hover = null)
    {
        _customTakenNodeSprite = taken;
        _customUntakenNodeSprite = untaken != null ? untaken : taken;
        _customEndingNodeSprite = ending != null ? ending : taken;
        _customHoverNodeSprite = hover;
        if (IsVisible) RebuildScenarioTree();
    }

    /// <summary>
    /// Configures custom sprite for the hover annotation tooltip box.
    /// </summary>
    public void SetCustomTooltipSprite(Sprite tooltipSprite)
    {
        _customTooltipSprite = tooltipSprite;
        if (_tooltipUI != null) _tooltipUI.SetCustomSprite(_customTooltipSprite);
    }

    /// <summary>
    /// Configures custom sprites for the flowchart connection arrows.
    /// </summary>
    public void SetCustomArrowSprites(Sprite arrowHead, Sprite arrowShaft = null)
    {
        _customArrowHeadSprite = arrowHead;
        _customArrowShaftSprite = arrowShaft;
        if (IsVisible) RebuildScenarioTree();
    }

    /// <summary>
    /// Configures a custom full-screen background sprite for the ScenarioSystem summary panel.
    /// </summary>
    public void SetCustomBackgroundSprite(Sprite backgroundSprite)
    {
        _customBackgroundSprite = backgroundSprite;
        if (_panelRoot != null)
        {
            Image bg = _panelRoot.GetComponent<Image>();
            if (bg != null)
            {
                if (_customBackgroundSprite != null)
                {
                    bg.sprite = _customBackgroundSprite;
                    bg.type = (_customBackgroundSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
                    bg.color = Color.white;
                }
                else
                {
                    bg.sprite = null;
                    bg.color = _backgroundColor;
                }
            }
        }
    }

    private void BindButtonListeners()
    {
        if (_toggleModeButton != null)
        {
            _toggleModeButton.onClick.RemoveListener(ToggleBranchMode);
            _toggleModeButton.onClick.AddListener(ToggleBranchMode);
        }

        if (_resetViewButton != null)
        {
            _resetViewButton.onClick.RemoveListener(OnResetViewClicked);
            _resetViewButton.onClick.AddListener(OnResetViewClicked);
        }

        if (_mainMenuButton != null)
        {
            _mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            _mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(HideScenarioTree);
            _closeButton.onClick.AddListener(HideScenarioTree);
        }
    }

    private Button CreateHeaderButton(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        string label,
        Color bgColor,
        out TextMeshProUGUI labelTmp)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        Sprite btnSprite = _customHeaderButtonSprite != null ? _customHeaderButtonSprite : ScenarioProceduralSprites.GetRoundedRectSprite();
        img.sprite = btnSprite;
        img.type = (btnSprite != null && btnSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
        img.color = _customHeaderButtonSprite != null ? Color.white : bgColor;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(6f, 2f);
        txtRect.offsetMax = new Vector2(-6f, -2f);

        labelTmp = txtObj.GetComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 14f;
        labelTmp.color = Color.white;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.raycastTarget = false;

        return btnObj.GetComponent<Button>();
    }
}
