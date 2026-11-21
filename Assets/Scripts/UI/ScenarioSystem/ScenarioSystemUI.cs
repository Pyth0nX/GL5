using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Main controller for the ScenarioSystem XY flowchart Canvas.
/// Renders the interactive 2D narrative decision tree across playthroughs,
/// opens a centered 70% detail screen when clicking any node, hides unreached endings so total ending count stays secret,
/// supports full XY drag-pan and scroll-wheel zoom, and prompts the player to start a new playthrough on close.
/// </summary>
public class ScenarioSystemUI : MonoBehaviour, IScrollHandler
{
    public static ScenarioSystemUI Instance { get; private set; }

    [Header("Per-Node Visibility & Annotation Overrides")]
    [Tooltip("Optional list of per-node overrides. Allows enabling/disabling whether specific NarrativeNodes appear in the ScenarioSystem summary screen.")]
    [SerializeField] private List<ScenarioNodeOverride> _nodeOverrides = new List<ScenarioNodeOverride>();

    [Header("Layout & 2D XY Navigation Settings")]
    [SerializeField] private Vector2 _decisionNodeSize = new Vector2(132f, 126f);
    [SerializeField] private Vector2 _untakenNodeSize = new Vector2(100f, 98f);
    [SerializeField] private Vector2 _endingNodeSize = new Vector2(154f, 138f);
    [SerializeField] private float _horizontalColumnSpacing = 265f;
    [SerializeField] private float _verticalNodeSpacing = 185f;
    [SerializeField] private float _minZoom = 0.45f;
    [SerializeField] private float _maxZoom = 1.5f;
    [SerializeField] private float _zoomStep = 0.12f;

    [Header("Situation Images (70% Detail Modal)")]
    [Tooltip("Optional ScriptableObject catalog mapping NarrativeNode IDs to situation artwork.")]
    [SerializeField] private ScenarioSituationImageCatalog _situationImageCatalog;

    [Tooltip("Optional inline list of situation illustrations for the 70% node inspection modal.")]
    [SerializeField] private List<ScenarioSituationImageEntry> _situationImages = new List<ScenarioSituationImageEntry>();

    [Header("Visual Palette Across Playthroughs")]
    [SerializeField] private Color _backgroundColor = new Color(0.149f, 0.149f, 0.149f, 1f); // #262626
    [SerializeField] private Color _takenArrowColor = new Color(0.96f, 0.82f, 0.34f, 1f);     // Bright Gold (Current Playthrough)
    [SerializeField] private Color _previousRunArrowColor = new Color(0.58f, 0.50f, 0.85f, 0.88f); // Lavender (Previous Playthroughs)
    [SerializeField] private Color _untakenArrowColor = new Color(0.45f, 0.45f, 0.50f, 0.58f);   // Muted Grey (Undiscovered)
    [SerializeField] private Color _dividerColor = new Color(0.85f, 0.85f, 0.88f, 0.55f);

    [Header("Custom Visual Sprites (Optional — falls back to procedural generation if null)")]
    [Tooltip("Optional custom sprite for nodes representing decisions taken. If unassigned, procedural rounded rect is used.")]
    [SerializeField] private Sprite _customTakenNodeSprite;

    [Tooltip("Optional custom sprite for untaken nodes ('???'). If unassigned, procedural rounded rect is used.")]
    [SerializeField] private Sprite _customUntakenNodeSprite;

    [Tooltip("Optional custom sprite for the ending nodes. If unassigned, procedural rounded rect or _customTakenNodeSprite is used.")]
    [SerializeField] private Sprite _customEndingNodeSprite;

    [Tooltip("Optional custom sprite for the node when hovered by mouse.")]
    [SerializeField] private Sprite _customHoverNodeSprite;

    [Header("Custom Arrow Sprites (Per Path State — points right +X)")]
    [Tooltip("Custom single-piece arrow sprite for paths taken in the CURRENT playthrough.")]
    [SerializeField] private Sprite _customTakenArrowSprite;

    [Tooltip("Custom single-piece arrow sprite for paths taken in PREVIOUS playthroughs. Falls back to Taken Arrow if unassigned.")]
    [SerializeField] private Sprite _customPreviousRunArrowSprite;

    [Tooltip("Custom single-piece arrow sprite for UNDISCOVERED paths leading to '???' nodes. Falls back to Taken Arrow if unassigned.")]
    [SerializeField] private Sprite _customUntakenArrowSprite;

    [Tooltip("Height / thickness for custom single-piece arrow sprites in UI units.")]
    [SerializeField] private float _customArrowHeight = 20f;

    [Tooltip("If true, custom arrow sprites are tinted with Current/Previous/Undiscovered Arrow Color. If false, they keep their native sprite colors.")]
    [SerializeField] private bool _tintCustomArrowSprites = false;

    [Tooltip("Alpha opacity applied when a previous-run arrow reuses the current arrow sprite as fallback without tinting.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _fallbackPreviousArrowAlpha = 0.8f;

    [Tooltip("Alpha opacity applied when an undiscovered arrow reuses the current arrow sprite as fallback without tinting.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _fallbackUntakenArrowAlpha = 0.5f;

    [Header("Legacy 2-Piece Arrow Overrides (Optional Fallback)")]
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
    [SerializeField] private TextMeshProUGUI _headerSummaryText;
    [SerializeField] private Button _resetViewButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _closeButton;

    private readonly List<GameObject> _spawnedGraphObjects = new List<GameObject>();
    private ScenarioNodeDetailModalUI _detailModalUI;
    private ScenarioReplayPromptModalUI _replayPromptModalUI;
    private GameObject _legendContainer;
    private GameObject _legendPreviousRow;
    private Button _zoomInButton;
    private Button _zoomOutButton;
    private float _currentZoom = 1f;
    private bool _isCurrentViewCampaignCompleted;
    private bool _isHierarchyInitialized;
    private float _previousTimeScale = 1f;
    private bool _hasCapturedTimeScale;

    public bool IsVisible => _panelRoot != null && _panelRoot.activeSelf;

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

        // Handle Escape in priority order: 70% detail modal -> replay prompt -> campaign replay prompt or close
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleCloseOrBackRequested();
            return;
        }

        // Allow keyboard WASD / Arrow XY panning and mouse-wheel zoom when no modal overlay is open
        bool modalOpen = IsAnyModalOpen();
        if (!modalOpen && Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                AdjustZoom(Mathf.Sign(scrollY) * _zoomStep);
            }
        }

        if (!modalOpen && _scrollRect != null && Keyboard.current != null)
        {
            float moveX = 0f;
            float moveY = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY -= 1f;

            if ((moveX != 0f || moveY != 0f) && _graphContentRect != null && _viewportRect != null)
            {
                float maxScrollX = Mathf.Max(1f, (_graphContentRect.rect.width * _currentZoom) - _viewportRect.rect.width);
                float maxScrollY = Mathf.Max(1f, (_graphContentRect.rect.height * _currentZoom) - _viewportRect.rect.height);
                float panSpeed = 950f * Time.unscaledDeltaTime;

                _scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                    _scrollRect.horizontalNormalizedPosition + (moveX * panSpeed / maxScrollX));
                _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                    _scrollRect.verticalNormalizedPosition + (moveY * panSpeed / maxScrollY));
            }
        }
    }

    private bool IsAnyModalOpen()
    {
        return (_detailModalUI != null && _detailModalUI.IsOpen) ||
               (_replayPromptModalUI != null && _replayPromptModalUI.IsOpen);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!IsVisible || IsAnyModalOpen())
        {
            return;
        }

        if (Mathf.Abs(eventData.scrollDelta.y) > 0.01f)
        {
            AdjustZoom(Mathf.Sign(eventData.scrollDelta.y) * _zoomStep);
        }
    }

    private void AdjustZoom(float delta)
    {
        SetZoom(_currentZoom + delta);
    }

    private void SetZoom(float targetZoom)
    {
        float clamped = Mathf.Clamp(targetZoom, _minZoom, _maxZoom);
        if (Mathf.Approximately(clamped, _currentZoom) && _graphContentRect != null &&
            Mathf.Approximately(_graphContentRect.localScale.x, clamped))
        {
            return;
        }

        float prevNormX = _scrollRect != null ? _scrollRect.horizontalNormalizedPosition : 0f;
        float prevNormY = _scrollRect != null ? _scrollRect.verticalNormalizedPosition : 0.5f;

        _currentZoom = clamped;
        if (_graphContentRect != null)
        {
            _graphContentRect.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
        }

        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(prevNormX);
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(prevNormY);
        }
    }

    private void HandleCloseOrBackRequested()
    {
        if (_detailModalUI != null && _detailModalUI.IsOpen)
        {
            _detailModalUI.Hide();
            return;
        }

        if (_replayPromptModalUI != null && _replayPromptModalUI.IsOpen)
        {
            _replayPromptModalUI.Hide();
            return;
        }

        if (_isCurrentViewCampaignCompleted)
        {
            ShowReplayPromptModal();
            return;
        }

        HideScenarioTree();
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

        if (_detailModalUI != null)
        {
            _detailModalUI.Hide();
        }
        if (_replayPromptModalUI != null)
        {
            _replayPromptModalUI.Hide();
        }

        _currentZoom = 1f;
        if (_graphContentRect != null)
        {
            _graphContentRect.localScale = Vector3.one;
        }

        RebuildScenarioTree();

        if (isActiveAndEnabled)
        {
            StartCoroutine(ScrollToStartNextFrame());
        }
    }

    /// <summary>
    /// Hides the ScenarioSystem summary screen and any open modal overlays,
    /// restoring gameplay timeScale and player inputs according to the active UI state.
    /// </summary>
    public void HideScenarioTree()
    {
        if (_detailModalUI != null)
        {
            _detailModalUI.Hide();
        }
        if (_replayPromptModalUI != null)
        {
            _replayPromptModalUI.Hide();
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

    private ScenarioGraphLayoutSettings BuildLayoutSettings()
    {
        return new ScenarioGraphLayoutSettings
        {
            decisionNodeSize = _decisionNodeSize,
            untakenNodeSize = _untakenNodeSize,
            endingNodeSize = _endingNodeSize,
            horizontalColumnSpacing = _horizontalColumnSpacing,
            verticalNodeSpacing = _verticalNodeSpacing
        };
    }

    /// <summary>
    /// Re-collects all decision data from DayManager, ReputationManager, DecisionDataManager, and MessagesManager
    /// and reconstructs all nodes and directed connection arrows.
    /// </summary>
    public void RebuildScenarioTree()
    {
        EnsureHierarchyBuilt();
        ClearSpawnedGraphObjects();

        if (_scrollRect != null)
        {
            _scrollRect.horizontal = true;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Elastic;
            _scrollRect.scrollSensitivity = 0f; // Mouse wheel is dedicated to XY zoom
        }

        ScenarioTreeModel model = ScenarioDataCollector.CollectTreeModel(_nodeOverrides);
        _isCurrentViewCampaignCompleted = model.isCampaignCompleted;
        UpdateHeaderSummary(model);
        UpdateLegendUI(model);

        List<ScenarioColumnData> storyColumns = new List<ScenarioColumnData>();
        if (model.rootColumn != null)
        {
            storyColumns.Add(model.rootColumn);
        }
        if (model.decisionColumns != null)
        {
            storyColumns.AddRange(model.decisionColumns);
        }

        bool hasEndings = model.endingColumn != null && model.endingColumn.nodes != null && model.endingColumn.nodes.Count > 0;
        if (storyColumns.Count == 0 && !hasEndings)
        {
            CreateEmptyStateLabel("No narrative decisions have been taken yet.");
            return;
        }

        ScenarioGraphLayoutSettings layoutSettings = BuildLayoutSettings();
        Vector2 viewportSize = _viewportRect != null ? _viewportRect.rect.size : Vector2.zero;

        // Compute organic 2D XY positions before instantiating nodes or connections
        ScenarioGraphLayoutResult layoutResult = ScenarioGraphLayoutSolver.ComputeLayout(
            storyColumns,
            hasEndings ? model.endingColumn : null,
            layoutSettings,
            viewportSize);

        if (_graphContentRect != null)
        {
            _graphContentRect.localScale = Vector3.one;
            _graphContentRect.sizeDelta = layoutResult.contentSize;
        }

        Dictionary<string, ScenarioNodeUI> spawnedNodesByKey = new Dictionary<string, ScenarioNodeUI>(System.StringComparer.Ordinal);
        List<ScenarioNodeUI> allSpawnedNodes = new List<ScenarioNodeUI>();

        // 1. Spawn story decision nodes in XY space
        for (int colIdx = 0; colIdx < storyColumns.Count; colIdx++)
        {
            ScenarioColumnData colData = storyColumns[colIdx];
            if (colData == null || colData.nodes == null || colData.nodes.Count == 0) continue;

            float colTopY = float.MinValue;
            float colSumX = 0f;
            int count = colData.nodes.Count;

            for (int n = 0; n < count; n++)
            {
                ScenarioOptionNodeData nodeData = colData.nodes[n];
                Vector2 size = ScenarioGraphLayoutSolver.GetNodeBoxSize(
                    nodeData,
                    colData.isEndingColumn,
                    colData.isRootEntryColumn,
                    layoutSettings);

                ScenarioNodeUI nodeUI = CreateNodeUI(nodeData, nodeData.graphPosition, size);
                allSpawnedNodes.Add(nodeUI);
                if (!string.IsNullOrEmpty(nodeData.nodeKey))
                {
                    spawnedNodesByKey[nodeData.nodeKey] = nodeUI;
                }

                colSumX += nodeData.graphPosition.x;
                float topEdge = nodeData.graphPosition.y + (size.y * 0.5f);
                if (topEdge > colTopY) colTopY = topEdge;
            }

            CreateColumnHeaderLabel(colData, colSumX / count, colTopY + 30f);
        }

        // 2. Vertical dashed separator before special Ending zone
        if (hasEndings)
        {
            DrawVerticalDashedDivider(layoutResult.dividerX, layoutResult.dividerMinY, layoutResult.dividerMaxY);

            ScenarioColumnData endingCol = model.endingColumn;
            float endingHeaderY = float.MinValue;
            float endingXSum = 0f;
            int endingCount = endingCol.nodes.Count;

            for (int e = 0; e < endingCount; e++)
            {
                ScenarioOptionNodeData endData = endingCol.nodes[e];
                Vector2 size = _endingNodeSize;
                ScenarioNodeUI endNodeUI = CreateNodeUI(endData, endData.graphPosition, size);
                allSpawnedNodes.Add(endNodeUI);
                if (!string.IsNullOrEmpty(endData.nodeKey))
                {
                    spawnedNodesByKey[endData.nodeKey] = endNodeUI;
                }

                endingXSum += endData.graphPosition.x;
                float topEdge = endData.graphPosition.y + (size.y * 0.5f);
                if (topEdge > endingHeaderY) endingHeaderY = topEdge;
            }

            CreateColumnHeaderLabel(endingCol, endingXSum / endingCount, endingHeaderY + 34f);
        }

        // Force canvas transform update at 1x scale before computing connection anchors
        Canvas.ForceUpdateCanvases();

        // 3. Draw directed XY connections (Undiscovered -> PreviousRun -> CurrentRun)
        DrawGraphEdges(model, allSpawnedNodes, spawnedNodesByKey);

        // Restore active zoom scale
        if (_graphContentRect != null)
        {
            _graphContentRect.localScale = new Vector3(_currentZoom, _currentZoom, 1f);
        }
    }

    private void DrawVerticalDashedDivider(float dividerX, float minY, float maxY)
    {
        if (_connectionsLayerRect == null) return;

        // Convert graph anchored Y (centered vertically inside _graphContentRect) to connections layer local coordinates
        const float dashHeight = 22f;
        const float gapHeight = 16f;
        const float dashWidth = 4f;

        for (float y = minY; y < maxY; y += (dashHeight + gapHeight))
        {
            float segLen = Mathf.Min(dashHeight, maxY - y);
            if (segLen < 4f) break;

            GameObject dashObj = new GameObject("DividerDash", typeof(RectTransform), typeof(Image));
            dashObj.transform.SetParent(_connectionsLayerRect, false);
            _spawnedGraphObjects.Add(dashObj);

            RectTransform rect = dashObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(dividerX, y + segLen * 0.5f);
            rect.sizeDelta = new Vector2(dashWidth, segLen);

            Image img = dashObj.GetComponent<Image>();
            img.color = _dividerColor;
            img.raycastTarget = false;
        }
    }

    private void UpdateHeaderSummary(ScenarioTreeModel model)
    {
        if (_headerSummaryText == null || model == null)
        {
            return;
        }

        int pct = Mathf.Clamp(Mathf.RoundToInt(model.discoveryPercent), 0, 100);

        if (model.isCampaignCompleted)
        {
            _headerSummaryText.text =
                $"SCENARIO SYSTEM  |  <color=#D8DF64>{model.reachedEndingTitle}</color>  |  " +
                $"Discovered: <color=#8BE0A4>{pct}%</color>  |  " +
                $"Final Safety: <color=#7CE08A>{model.finalReputation}/100</color>  |  " +
                $"<color=#B0B6C4><size=85%>Click node: details  •  Drag: XY pan  •  Wheel: zoom</size></color>";
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
                    $"SCENARIO SYSTEM (PREVIEW)  |  Day {currentDay}  |  " +
                    $"Discovered: <color=#8BE0A4>{pct}%</color>  |  " +
                    $"Safety: <color=#7CE08A>{model.finalReputation}/100</color>  |  " +
                    $"<color=#B0B6C4><size=85%>Click node: details  •  Drag: XY pan  •  Wheel: zoom</size></color>";
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

    private void DrawGraphEdges(
        ScenarioTreeModel model,
        List<ScenarioNodeUI> allSpawnedNodes,
        Dictionary<string, ScenarioNodeUI> spawnedNodesByKey)
    {
        if (_connectionsLayerRect == null || model == null)
        {
            return;
        }

        // Draw directed edges in strict Z-order: Undiscovered -> PreviousRun -> CurrentRun (never draw outgoing arrows from '???' nodes)
        ScenarioNodeVisitState[] drawOrder =
        {
            ScenarioNodeVisitState.Undiscovered,
            ScenarioNodeVisitState.PreviousRun,
            ScenarioNodeVisitState.CurrentRun
        };

        for (int pass = 0; pass < drawOrder.Length; pass++)
        {
            ScenarioNodeVisitState targetState = drawOrder[pass];
            for (int e = 0; e < model.edges.Count; e++)
            {
                ScenarioEdgeData edge = model.edges[e];
                if (edge == null || edge.visitState != targetState) continue;

                if (!spawnedNodesByKey.TryGetValue(edge.fromNodeKey, out ScenarioNodeUI fromUI) || fromUI == null) continue;
                if (!spawnedNodesByKey.TryGetValue(edge.toNodeKey, out ScenarioNodeUI toUI) || toUI == null) continue;

                Vector3 targetCenterWorld = toUI.RectTransform.TransformPoint(toUI.RectTransform.rect.center);
                Vector3 sourceCenterWorld = fromUI.RectTransform.TransformPoint(fromUI.RectTransform.rect.center);

                Vector3 startWorld = fromUI.GetPerimeterConnectionWorldPoint(targetCenterWorld);
                Vector3 endWorld = toUI.GetPerimeterConnectionWorldPoint(sourceCenterWorld);

                Vector2 startLocal = _connectionsLayerRect.InverseTransformPoint(startWorld);
                Vector2 endLocal = _connectionsLayerRect.InverseTransformPoint(endWorld);

                Color arrowColor = targetState switch
                {
                    ScenarioNodeVisitState.CurrentRun => _takenArrowColor,
                    ScenarioNodeVisitState.PreviousRun => _previousRunArrowColor,
                    _ => _untakenArrowColor
                };
                float thickness = targetState switch
                {
                    ScenarioNodeVisitState.CurrentRun => 4.2f,
                    ScenarioNodeVisitState.PreviousRun => 3.0f,
                    _ => 2.2f
                };

                CreateDirectedArrow(startLocal, endLocal, arrowColor, thickness, targetState);
            }
        }
    }

    /// <summary>
    /// Creates a UI directed arrow pointing from startPos to endPos inside _connectionsLayerRect.
    /// Supports distinct single-piece custom arrow sprites for CurrentRun, PreviousRun, and Undiscovered paths,
    /// falling back to _customTakenArrowSprite or the procedural 2-piece arrow when unassigned.
    /// </summary>
    private void CreateDirectedArrow(
        Vector2 startPos,
        Vector2 endPos,
        Color color,
        float thickness,
        ScenarioNodeVisitState visitState = ScenarioNodeVisitState.CurrentRun)
    {
        Vector2 delta = endPos - startPos;
        float fullDistance = delta.magnitude;
        if (fullDistance < 8f)
        {
            return;
        }

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // Resolve single custom arrow sprite according to the path state (points right / +X by default)
        Sprite customArrowSprite = null;
        bool isUsingFallbackTakenSprite = false;

        switch (visitState)
        {
            case ScenarioNodeVisitState.CurrentRun:
                customArrowSprite = _customTakenArrowSprite;
                break;

            case ScenarioNodeVisitState.PreviousRun:
                if (_customPreviousRunArrowSprite != null)
                {
                    customArrowSprite = _customPreviousRunArrowSprite;
                }
                else if (_customTakenArrowSprite != null)
                {
                    customArrowSprite = _customTakenArrowSprite;
                    isUsingFallbackTakenSprite = true;
                }
                break;

            default:
                if (_customUntakenArrowSprite != null)
                {
                    customArrowSprite = _customUntakenArrowSprite;
                }
                else if (_customTakenArrowSprite != null)
                {
                    customArrowSprite = _customTakenArrowSprite;
                    isUsingFallbackTakenSprite = true;
                }
                break;
        }

        // If a single custom arrow sprite is available, instantiate 1 single GameObject for the entire arrow
        if (customArrowSprite != null)
        {
            string arrowName = visitState switch
            {
                ScenarioNodeVisitState.CurrentRun => "CustomArrow_Current",
                ScenarioNodeVisitState.PreviousRun => "CustomArrow_Previous",
                _ => "CustomArrow_Undiscovered"
            };
            GameObject arrowObj = new GameObject(arrowName, typeof(RectTransform), typeof(Image));
            arrowObj.transform.SetParent(_connectionsLayerRect, false);
            _spawnedGraphObjects.Add(arrowObj);

            RectTransform rect = arrowObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (startPos + endPos) * 0.5f;
            float arrowHeight = _customArrowHeight > 0f ? _customArrowHeight : thickness * 5f;
            rect.sizeDelta = new Vector2(fullDistance, arrowHeight);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            Image img = arrowObj.GetComponent<Image>();
            img.sprite = customArrowSprite;
            img.type = (customArrowSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;

            if (_tintCustomArrowSprites)
            {
                img.color = color;
            }
            else if (isUsingFallbackTakenSprite)
            {
                float alpha = visitState == ScenarioNodeVisitState.PreviousRun
                    ? _fallbackPreviousArrowAlpha
                    : _fallbackUntakenArrowAlpha;
                img.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                img.color = Color.white;
            }

            img.raycastTarget = false;
            return;
        }

        // Legacy / Procedural Fallback: 2-piece arrow (Shaft + Head)
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
            : (nodeData.IsDiscovered ? _customTakenNodeSprite : _customUntakenNodeSprite);

        ScenarioNodeUI nodeUI = nodeObj.GetComponent<ScenarioNodeUI>();
        nodeUI.ConfigureReferences(nodeRect, bgImage, outline, labelTmp);
        nodeUI.Initialize(
            nodeData,
            null,
            null,
            fallbackSprite,
            _customHoverNodeSprite,
            _tintCustomNodeSprites,
            OnNodeClicked);

        return nodeUI;
    }

    private void OnNodeClicked(ScenarioNodeUI clickedNode)
    {
        if (clickedNode == null || clickedNode.NodeData == null)
        {
            return;
        }

        EnsureRuntimeOverlaysAndControls();
        if (_detailModalUI == null)
        {
            return;
        }

        Sprite resolvedSprite = ResolveSituationImage(clickedNode.NodeData);
        _detailModalUI.ShowForNode(clickedNode.NodeData, resolvedSprite);
    }

    private Sprite ResolveSituationImage(ScenarioOptionNodeData data)
    {
        if (data == null) return null;

        if (data.situationImage != null)
        {
            return data.situationImage;
        }

        Sprite inlineMatch = ScenarioSituationImageCatalog.ResolveFromEntries(
            _situationImages,
            data.parentNodeId,
            data.optionIndex);
        if (inlineMatch != null)
        {
            return inlineMatch;
        }

        if (_situationImageCatalog != null)
        {
            Sprite catalogMatch = _situationImageCatalog.ResolveImage(data.parentNodeId, data.optionIndex);
            if (catalogMatch != null)
            {
                return catalogMatch;
            }
        }

        return ScenarioSituationImageCatalog.ResolveFallbackFromResources(data.parentNodeId, data.optionIndex);
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
        SetZoom(1f);
        if (_scrollRect != null)
        {
            _scrollRect.horizontalNormalizedPosition = 0f;
            _scrollRect.verticalNormalizedPosition = 0.5f;
        }
    }

    private void OnMainMenuClicked()
    {
        if (_isCurrentViewCampaignCompleted && SaveManager.HasInstance)
        {
            SaveManager.Instance.RecordCurrentRunScenarioProgress(incrementCompletedPlaythrough: false);
            if (SaveManager.Instance.HasActiveSlot)
            {
                SaveManager.Instance.SaveCurrentSlot();
            }
        }

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

    private void OnStartNewPlaythroughClicked()
    {
        if (_replayPromptModalUI != null)
        {
            _replayPromptModalUI.Hide();
        }

        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.StartNewPlaythroughInCurrentSlot();
            return;
        }

        HideScenarioTree();
        SaveManager.ResetRuntimeGameplayManagers();
        PlayerSpawnManager.Instance?.QueueSpawn(PlayerSpawnManager.SpawnerWakingUp);
        UnityEngine.SceneManagement.SceneManager.LoadScene("Home");
    }

    private void ShowReplayPromptModal()
    {
        EnsureRuntimeOverlaysAndControls();
        _replayPromptModalUI?.Show();
    }

    private void HideReplayPromptModal()
    {
        _replayPromptModalUI?.Hide();
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
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 500;

        if (transform.parent is RectTransform && transform is RectTransform selfRect)
        {
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;
            selfRect.localScale = Vector3.one;
        }

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
        if (_isHierarchyInitialized && _panelRoot != null && _graphContentRect != null)
        {
            EnforceFullScreenLayout();
            EnsureRuntimeOverlaysAndControls();
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
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 500;

        if (transform.parent is RectTransform && transform is RectTransform selfRect)
        {
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;
            selfRect.localScale = Vector3.one;
        }

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
        if (_panelRoot != null && _graphContentRect != null && _connectionsLayerRect != null && _nodesLayerRect != null)
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
            EnsureRuntimeOverlaysAndControls();
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

        // Header Action Buttons (Reset View, Main Menu, Close — no Option A mode toggle)
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

        _isHierarchyInitialized = true;
        EnsureRuntimeOverlaysAndControls();
        BindButtonListeners();
    }

    private void EnsureRuntimeOverlaysAndControls()
    {
        if (_panelRoot == null) return;

        // Ensure HeaderSummaryText never overlaps header buttons
        if (_headerSummaryText != null)
        {
            RectTransform summaryRect = _headerSummaryText.rectTransform;
            summaryRect.anchorMax = new Vector2(0.66f, 1f);
            _headerSummaryText.textWrappingMode = TextWrappingModes.Normal;
            _headerSummaryText.overflowMode = TextOverflowModes.Ellipsis;
        }

        // Deactivate any legacy Option A / hover-tooltip objects present on older serialized prefabs
        Transform legacyTooltip = _viewportRect != null ? _viewportRect.Find("ScenarioAnnotationTooltip") : null;
        if (legacyTooltip != null)
        {
            legacyTooltip.gameObject.SetActive(false);
        }

        // Ensure compact Zoom + / - buttons exist next to Reset View
        Transform headerTransform = _panelRoot.transform.Find("HeaderBar");
        if (headerTransform != null)
        {
            Transform legacyModeBtn = headerTransform.Find("ToggleModeButton");
            if (legacyModeBtn != null)
            {
                legacyModeBtn.gameObject.SetActive(false);
            }

            if (_zoomInButton == null)
            {
                Transform existing = headerTransform.Find("ZoomInButton");
                _zoomInButton = existing != null
                    ? existing.GetComponent<Button>()
                    : CreateHeaderButton(
                        headerTransform,
                        "ZoomInButton",
                        new Vector2(-375f, 0f),
                        new Vector2(40f, 42f),
                        "+",
                        new Color(0.28f, 0.28f, 0.28f, 1f),
                        out _);
            }

            if (_zoomOutButton == null)
            {
                Transform existing = headerTransform.Find("ZoomOutButton");
                _zoomOutButton = existing != null
                    ? existing.GetComponent<Button>()
                    : CreateHeaderButton(
                        headerTransform,
                        "ZoomOutButton",
                        new Vector2(-421f, 0f),
                        new Vector2(40f, 42f),
                        "-",
                        new Color(0.28f, 0.28f, 0.28f, 1f),
                        out _);
            }
        }

        // Ensure 70% Detail Modal UI exists
        if (_detailModalUI == null)
        {
            _detailModalUI = _panelRoot.GetComponentInChildren<ScenarioNodeDetailModalUI>(true);
            if (_detailModalUI == null)
            {
                GameObject modalObj = new GameObject("ScenarioNodeDetailModal", typeof(RectTransform), typeof(ScenarioNodeDetailModalUI));
                modalObj.transform.SetParent(_panelRoot.transform, false);
                _detailModalUI = modalObj.GetComponent<ScenarioNodeDetailModalUI>();
            }
            _detailModalUI.EnsureBuilt(_panelRoot.transform);
        }

        // Ensure Post-Campaign Replay Prompt Modal exists
        if (_replayPromptModalUI == null)
        {
            _replayPromptModalUI = _panelRoot.GetComponentInChildren<ScenarioReplayPromptModalUI>(true);
            if (_replayPromptModalUI == null)
            {
                Transform existingPrompt = _panelRoot.transform.Find("ScenarioReplayPromptModal");
                GameObject promptObj = existingPrompt != null
                    ? existingPrompt.gameObject
                    : new GameObject("ScenarioReplayPromptModal", typeof(RectTransform));
                if (existingPrompt == null)
                {
                    promptObj.transform.SetParent(_panelRoot.transform, false);
                }

                _replayPromptModalUI = promptObj.GetComponent<ScenarioReplayPromptModalUI>();
                if (_replayPromptModalUI == null)
                {
                    _replayPromptModalUI = promptObj.AddComponent<ScenarioReplayPromptModalUI>();
                }
            }
        }
        _replayPromptModalUI.EnsureBuilt(
            _panelRoot.transform,
            _takenArrowColor,
            OnStartNewPlaythroughClicked,
            OnMainMenuClicked);

        // Ensure bottom-left Path Legend exists
        if (_legendContainer == null && _viewportRect != null)
        {
            Transform existingLegend = _viewportRect.Find("PathLegend");
            _legendContainer = existingLegend != null ? existingLegend.gameObject : BuildPathLegend(_viewportRect);
        }
    }

    private GameObject BuildPathLegend(Transform parent)
    {
        GameObject legendObj = new GameObject("PathLegend", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        legendObj.transform.SetParent(parent, false);

        RectTransform rect = legendObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(20f, 20f);
        rect.sizeDelta = new Vector2(228f, 92f);

        Image bg = legendObj.GetComponent<Image>();
        bg.sprite = ScenarioProceduralSprites.GetRoundedRectSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.10f, 0.10f, 0.12f, 0.88f);
        bg.raycastTarget = false;

        VerticalLayoutGroup vlg = legendObj.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 8, 8);
        vlg.spacing = 4f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;

        CreateLegendRow(legendObj.transform, _takenArrowColor, "Current Playthrough");
        _legendPreviousRow = CreateLegendRow(legendObj.transform, _previousRunArrowColor, "Previous Playthroughs");
        CreateLegendRow(legendObj.transform, _untakenArrowColor, "Undiscovered (???)");

        return legendObj;
    }

    private GameObject CreateLegendRow(Transform parent, Color swatchColor, string labelText)
    {
        GameObject rowObj = new GameObject($"Legend_{labelText}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowObj.transform.SetParent(parent, false);

        HorizontalLayoutGroup hlg = rowObj.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject swatchObj = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        swatchObj.transform.SetParent(rowObj.transform, false);
        swatchObj.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 14f);
        Image swatchImg = swatchObj.GetComponent<Image>();
        swatchImg.sprite = ScenarioProceduralSprites.GetRoundedRectSprite();
        swatchImg.type = Image.Type.Sliced;
        swatchImg.color = swatchColor;
        swatchImg.raycastTarget = false;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(rowObj.transform, false);
        txtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(178f, 20f);
        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.fontSize = 13.5f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;

        return rowObj;
    }

    private void UpdateLegendUI(ScenarioTreeModel model)
    {
        if (_legendPreviousRow != null && model != null)
        {
            _legendPreviousRow.SetActive(model.hasPreviousRunHistory);
        }
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
    /// Configures custom single-piece arrow sprites for the flowchart connections:
    /// current playthrough path, undiscovered ('???') paths, and previous playthrough paths.
    /// </summary>
    public void SetCustomArrowSprites(Sprite takenArrow, Sprite untakenArrow = null, Sprite previousRunArrow = null)
    {
        _customTakenArrowSprite = takenArrow;
        _customUntakenArrowSprite = untakenArrow;
        _customPreviousRunArrowSprite = previousRunArrow;
        if (IsVisible) RebuildScenarioTree();
    }

    /// <summary>
    /// Legacy method for configuring procedural 2-piece arrow sprites (head and shaft).
    /// </summary>
    public void SetLegacyCustomArrowSprites(Sprite arrowHead, Sprite arrowShaft = null)
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
            _closeButton.onClick.RemoveListener(HandleCloseOrBackRequested);
            _closeButton.onClick.AddListener(HandleCloseOrBackRequested);
        }

        if (_zoomInButton != null)
        {
            _zoomInButton.onClick.RemoveListener(HandleZoomInClicked);
            _zoomInButton.onClick.AddListener(HandleZoomInClicked);
        }

        if (_zoomOutButton != null)
        {
            _zoomOutButton.onClick.RemoveListener(HandleZoomOutClicked);
            _zoomOutButton.onClick.AddListener(HandleZoomOutClicked);
        }
    }

    private void HandleZoomInClicked() => AdjustZoom(_zoomStep);
    private void HandleZoomOutClicked() => AdjustZoom(-_zoomStep);

    private Button CreateHeaderButton(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        string label,
        Color bgColor,
        out TextMeshProUGUI labelTmp)
    {
        Button btn = ScenarioUiFactory.CreateRoundedButton(
            parent,
            name,
            label,
            bgColor,
            Color.white,
            14f,
            FontStyles.Normal,
            null,
            out labelTmp);

        RectTransform rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        if (_customHeaderButtonSprite != null)
        {
            Image img = btn.GetComponent<Image>();
            img.sprite = _customHeaderButtonSprite;
            img.type = (_customHeaderButtonSprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Color.white;
        }

        return btn;
    }
}
