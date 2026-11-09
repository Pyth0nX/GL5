using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; } // Singleton instance

    [SerializeField]
    private GameObject _dialogueBox; // The dialogue box UI element
    [SerializeField]
    private TextMeshProUGUI _characterNameText; // The text component for displaying the character's name
    [SerializeField]
    private TextMeshProUGUI _dialogueText; // The text component for displaying dialogue

    private List<Message> _currentDialogues; // The list of dialogues for the current conversation
    private string _currentContact; // The contact that is currently being interacted with
    private int _currentDialogueIndex = 0; // The index of the current dialogue being displayed
    private bool _isEndNode = false;
    private bool _hasActiveDecision = false;

    private Button _dialogueAdvanceButton;
    private CanvasGroup _dialogueCanvasGroup;
    private bool _standaloneMonologueActive;
    private Action _standaloneMonologueClosed;
    private Canvas _dialogueCanvas;
    private bool _previousOverrideSorting;
    private int _previousSortingOrder;

    public bool HasActiveDecision => _hasActiveDecision;

    public void SetHasActiveDecision(bool hasActiveDecision)
    {
        _hasActiveDecision = hasActiveDecision;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureDialogueClick();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("Conversation_Started", StartDialogue);
        }
    }

    private void OnDestroy()
    {
        if (_dialogueAdvanceButton != null)
        {
            _dialogueAdvanceButton.onClick.RemoveListener(ContinueDialogue);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("Conversation_Started", StartDialogue);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (DebugCampaignPanelUI.Instance != null && DebugCampaignPanelUI.Instance.IsOpen)
        {
            return;
        }

        if (_dialogueBox == null || !_dialogueBox.activeSelf)
        {
            return;
        }

        if (!_standaloneMonologueActive && _hasActiveDecision)
        {
            return;
        }

        if (InputManager.Instance != null && InputManager.Instance.ContinueDialogueWasPressed())
        {
            ContinueDialogue();
        }
    }

    public void StartDialogue()
    {
        Debug.Log("[DialogueManager] Dialogue activated");

        if (_dialogueAdvanceButton == null && _dialogueBox != null)
        {
            ConfigureDialogueClick();
        }

        // Resolve entry redirects based on Reputation/Safety before starting the node
        NarrativeNode currentNode = MessagesManager.Instance != null
            ? MessagesManager.Instance.ResolveAndGetCurrentContactNode()
            : null;

        if (currentNode == null)
        {
            RecoverFromInvalidDialogue("Cannot start dialogue because the current contact has no valid narrative node.");
            return;
        }

        _currentDialogueIndex = 0;
        _currentDialogues = currentNode.GetActiveDialogues();
        _currentContact = MessagesManager.Instance.GetCurrentContactName();
        _isEndNode = currentNode.IsEndNode();

        if (_dialogueBox != null)
        {
            _dialogueBox.SetActive(true);
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
        }

        // Choice nodes often contain an introductory exchange. Keep the choice panel hidden
        // until all lines in the node have been displayed, then EndDialogue reveals the options.
        SetHasActiveDecision(false);
        SetChoiceMode(false);
        MessagesManager.Instance.GetOptionsChanger()?.ActiveDecisionsPanel(false);

        ContinueDialogue();
    }

    /// <summary>
    /// Shows a one-line inner monologue in the dialogue box without changing the active narrative contact
    /// or forcing the phone to close. E or a click on the dialogue box dismisses it.
    /// </summary>
    public bool ShowInnerMonologue(string text, Action onClosed = null)
    {
        return ShowStandaloneDialogue("[Luna]", text, onClosed);
    }

    /// <summary>
    /// Shows a narration line in the dialogue box while preserving the active phone conversation.
    /// E or a click dismisses it, then the caller can resume the remaining phone messages.
    /// </summary>
    public bool ShowNarration(string text, Action onClosed = null)
    {
        return ShowStandaloneDialogue("Narration", text, onClosed);
    }

    /// <summary>
    /// Shows a self-contained world dialogue without changing the active narrative contact.
    /// Intended for ambient NPCs whose lines must not advance or interrupt a story conversation.
    /// </summary>
    public bool ShowStandaloneLine(string speaker, string text, Action onClosed = null)
    {
        return ShowStandaloneDialogue(speaker, text, onClosed);
    }

    private bool ShowStandaloneDialogue(string speaker, string text, Action onClosed)
    {
        if (_dialogueBox == null || string.IsNullOrWhiteSpace(text) ||
            _dialogueBox.activeSelf || _standaloneMonologueActive)
        {
            return false;
        }

        if (_dialogueAdvanceButton == null)
        {
            ConfigureDialogueClick();
        }

        _standaloneMonologueActive = true;
        _standaloneMonologueClosed = onClosed;
        _currentDialogueIndex = 0;
        _currentDialogues = null;
        _isEndNode = false;

        if (_characterNameText != null) _characterNameText.text = speaker;
        if (_dialogueText != null) _dialogueText.text = text;
        SetChoiceMode(false);

        _dialogueCanvas = _dialogueBox.GetComponentInParent<Canvas>();
        if (_dialogueCanvas != null)
        {
            _previousOverrideSorting = _dialogueCanvas.overrideSorting;
            _previousSortingOrder = _dialogueCanvas.sortingOrder;
            _dialogueCanvas.overrideSorting = true;
            _dialogueCanvas.sortingOrder = 100;
        }

        _dialogueBox.SetActive(true);
        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
        }
        return true;
    }

    public void ContinueDialogue()
    {
        if (_dialogueBox == null || !_dialogueBox.activeSelf)
        {
            return;
        }

        if (_standaloneMonologueActive)
        {
            EndStandaloneMonologue();
            return;
        }

        if (_hasActiveDecision)
        {
            return;
        }

        NarrativeNode currentNode = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetCurrentContactNarrativeNode()
            : null;

        if (currentNode == null)
        {
            RecoverFromInvalidDialogue("Cannot continue dialogue because the current contact has no valid narrative node.");
            return;
        }

        if (_currentDialogues == null || _currentDialogueIndex >= _currentDialogues.Count)
        {
            EndDialogue();
            return;
        }

        Message currentMessage = _currentDialogues[_currentDialogueIndex];

        string speaker = "";
        if (currentMessage.Sender() == Sender.Player)
        {
            speaker = GameManager.Instance != null ? GameManager.Instance.GetPlayerName() : "Luna";
        }
        else
        {
            if (!string.IsNullOrEmpty(currentMessage.SpeakerName()))
            {
                speaker = currentMessage.SpeakerName();
            }
            else
            {
                speaker = _currentContact;
            }
        }

        if (_characterNameText != null) _characterNameText.text = speaker;
        if (_dialogueText != null) _dialogueText.text = currentMessage.Content();
        _currentDialogueIndex++;
    }

    private void EndStandaloneMonologue()
    {
        _standaloneMonologueActive = false;
        if (_dialogueBox != null)
        {
            _dialogueBox.SetActive(false);
        }
        _currentDialogueIndex = 0;
        _currentDialogues = null;

        if (_dialogueCanvas != null)
        {
            _dialogueCanvas.overrideSorting = _previousOverrideSorting;
            _dialogueCanvas.sortingOrder = _previousSortingOrder;
            _dialogueCanvas = null;
        }

        Action closed = _standaloneMonologueClosed;
        _standaloneMonologueClosed = null;
        closed?.Invoke();
    }

    private void EndDialogue()
    {
        NarrativeNode currentNode = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetCurrentContactNarrativeNode()
            : null;

        if (currentNode == null)
        {
            RecoverFromInvalidDialogue("Cannot end dialogue because the current contact has no valid narrative node.");
            return;
        }

        // If the current node has options, show the decisions panel now that all introductory lines have played
        if (currentNode.HasOptions())
        {
            SetHasActiveDecision(true);
            SetChoiceMode(true);
            OptionsChanger optionsChanger = MessagesManager.Instance.GetOptionsChanger();
            if (optionsChanger != null)
            {
                optionsChanger.SetCurrentNode(currentNode);
                optionsChanger.ActiveDecisionsPanel(true);
            }
            return;
        }

        bool isLoopNode = currentNode.IsLoop();
        bool reachedEndNode = _isEndNode || isLoopNode;
        _currentDialogueIndex = 0;
        _currentDialogues = null;

        if (reachedEndNode)
        {
            SetHasActiveDecision(false);
            SetChoiceMode(false);
            if (_dialogueBox != null)
            {
                _dialogueBox.SetActive(false);
            }
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ChangeState(0);
            }
            ReturnPlayerToIdle();
        }

        // 1. Apply any Reputation/Safety impact configured upon finishing the node
        currentNode.ApplyEndReputationImpact();

        // 2. Publish node end event
        PublishNodeEndEvent(currentNode);

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null)
        {
            if (!reachedEndNode)
            {
                RecoverFromInvalidDialogue("Cannot advance dialogue because no current contact is selected.");
            }
            return;
        }

        // If this node is configured as a loop (e.g. default recurring NPC phrase), reset back to the loop target
        // node so subsequent interactions replay the dialogue identically.
        if (isLoopNode)
        {
            int loopTargetId = currentNode.GetLoopTargetNodeId();
            loopTargetId = MessagesManager.Instance.ResolveEntryRedirects(loopTargetId);
            currentContact.SetNarrativeNode(loopTargetId);
            currentContact.SetCurrentMessageIndex(0);
            _isEndNode = false;
            return;
        }

        // 3. Resolve the next node by evaluating conditional reputation branches
        int nextNodeId = currentNode.ResolveNextNode();
        if (nextNodeId < 0)
        {
            if (reachedEndNode)
            {
                currentContact.SetNarrativeNode(-1);
                currentContact.SetCurrentMessageIndex(0);
                _isEndNode = false;
                return;
            }

            RecoverFromInvalidDialogue($"Narrative node {currentNode.ID()} ended without a valid next node.");
            return;
        }

        nextNodeId = MessagesManager.Instance.ResolveEntryRedirects(nextNodeId);
        currentContact.SetNarrativeNode(nextNodeId);
        currentContact.SetCurrentMessageIndex(0);

        if (reachedEndNode)
        {
            _isEndNode = false;
            return;
        }

        NarrativeNode nextNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (nextNode != null)
        {
            SetHasActiveDecision(false);
            StartDialogue();
        }
        else
        {
            RecoverFromInvalidDialogue($"Target narrative node {nextNodeId} was not found.");
        }
    }

    private void ConfigureDialogueClick()
    {
        if (_dialogueBox == null)
        {
            return;
        }

        _dialogueAdvanceButton = _dialogueBox.GetComponent<Button>();
        if (_dialogueAdvanceButton == null)
        {
            _dialogueAdvanceButton = _dialogueBox.AddComponent<Button>();
        }

        _dialogueAdvanceButton.targetGraphic = _dialogueBox.GetComponent<Graphic>();
        _dialogueAdvanceButton.transition = Selectable.Transition.None;
        Navigation navigation = _dialogueAdvanceButton.navigation;
        navigation.mode = Navigation.Mode.None;
        _dialogueAdvanceButton.navigation = navigation;
        _dialogueAdvanceButton.onClick.RemoveListener(ContinueDialogue);
        _dialogueAdvanceButton.onClick.AddListener(ContinueDialogue);

        _dialogueCanvasGroup = _dialogueBox.GetComponent<CanvasGroup>();
        if (_dialogueCanvasGroup == null)
        {
            _dialogueCanvasGroup = _dialogueBox.AddComponent<CanvasGroup>();
        }

        SetChoiceMode(false);
    }

    public void SetChoiceMode(bool isChoosing)
    {
        if (_dialogueAdvanceButton != null)
        {
            _dialogueAdvanceButton.interactable = !isChoosing;
        }

        if (_dialogueCanvasGroup != null)
        {
            // While choice buttons are layered over the dialogue box, prevent the dialogue background
            // from consuming pointer events meant for the decision buttons.
            _dialogueCanvasGroup.blocksRaycasts = !isChoosing;
        }
    }

    private void RecoverFromInvalidDialogue(string reason)
    {
        Debug.LogWarning($"[DialogueManager] {reason}");
        _currentDialogueIndex = 0;
        _currentDialogues = null;
        _isEndNode = false;
        _standaloneMonologueActive = false;
        _standaloneMonologueClosed = null;
        SetHasActiveDecision(false);
        SetChoiceMode(false);

        if (_dialogueBox != null)
        {
            _dialogueBox.SetActive(false);
        }

        MessagesManager.Instance?.GetOptionsChanger()?.ActiveDecisionsPanel(false);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ChangeState(0);
        }

        ReturnPlayerToIdle();
    }

    private void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }

    private static void PublishNodeEndEvent(NarrativeNode node)
    {
        if (node == null || string.IsNullOrEmpty(node.EventOnEnd()) || EventBus.Instance == null)
            return;

        EventBus.Instance.Publish(node.EventOnEnd());

        if (node.EndEventDataType() != EventDataType.NONE)
        {
            object val = node.EventDataValue();
            if (val == null) return;

            switch (node.EndEventDataType())
            {
                case EventDataType.INT:
                    EventBus.Instance.Publish<int>(node.EventOnEnd(), (int)val);
                    break;
                case EventDataType.FLOAT:
                    EventBus.Instance.Publish<float>(node.EventOnEnd(), (float)val);
                    break;
                case EventDataType.BOOL:
                    EventBus.Instance.Publish<bool>(node.EventOnEnd(), (bool)val);
                    break;
                case EventDataType.STRING:
                    EventBus.Instance.Publish<string>(node.EventOnEnd(), (string)val);
                    break;
            }
        }
    }
}
