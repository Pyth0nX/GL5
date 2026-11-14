using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueButton : MonoBehaviour
{
    private Button _button;
    private float _timer = 0f;
    private Image _image;
    private TextMeshProUGUI _textMesh;
    private bool _narrationDialogueActive;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _image = GetComponent<Image>();
        _textMesh = GetComponentInChildren<TextMeshProUGUI>();

        if (_button != null)
        {
            _button.onClick.AddListener(OnDialogueButtonPressed);
        }
    }

    private void Update()
    {
        UpdateButtonVisibility();
        CheckInput();
    }

    private void CheckInput()
    {
        if (MessagesManager.Instance == null) return;
        if (_narrationDialogueActive) return;

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null || !currentContact.HasNewMessages()) return;

        bool inputPressed = false;
        if (InputManager.Instance != null)
        {
            inputPressed = InputManager.Instance.InteractWasPressed() ||
                           InputManager.Instance.LeftClickWasPressed() ||
                           InputManager.Instance.ContinueDialogueWasPressed();
        }

        if (!inputPressed)
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) inputPressed = true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) inputPressed = true;
        }

        if (!inputPressed) return;

        // If the player response button is active and waiting for input, trigger sending the message
        if (_button != null && _button.interactable && _image != null && _image.enabled)
        {
            OnDialogueButtonPressed();
        }
        else
        {
            // If waiting for the 1-second NPC pause timer, fast-forward it
            _timer = Mathf.Max(_timer, 1f);
        }
    }

    private void SetVisualsActive(bool active)
    {
        if (_image != null) _image.enabled = active;
        if (_textMesh != null) _textMesh.enabled = active;
        if (_button != null) _button.interactable = active;
    }

    private void UpdateButtonVisibility()
    {
        if (MessagesManager.Instance == null)
            return;

        if (_narrationDialogueActive)
        {
            SetVisualsActive(false);
            _timer = 0f;
            return;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null)
        {
            SetVisualsActive(false);
            if (_button != null) _button.gameObject.SetActive(false);
            return;
        }

        if (currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.ResolveAndGetCurrentContactNode();
            if (currentNode != null)
            {
                // If a message is currently animating in the UI, wait for it to finish
                if (MessagesManager.Instance.GetMessagesUI() != null && MessagesManager.Instance.GetMessagesUI().IsAnimating)
                {
                    SetVisualsActive(false);
                    _timer = 0f;
                    return;
                }

                List<Message> activeDialogues = currentNode.GetActiveDialogues();
                if (currentContact.CurrentMessageIndex() < activeDialogues.Count)
                {
                    // Keep the GameObject active so Update() continues running
                    if (_button != null) _button.gameObject.SetActive(true);

                    Message nextMsg = activeDialogues[currentContact.CurrentMessageIndex()];
                    if (MessagesManager.IsNarration(nextMsg))
                    {
                        SetVisualsActive(false);
                        _timer = 0f;
                        ShowNarration(currentContact, nextMsg);
                        return;
                    }

                    if (nextMsg.Sender() == Sender.NPC)
                    {
                        // It's the NPC's turn. Hide the button visually and wait 1 second.
                        SetVisualsActive(false);

                        _timer += Time.deltaTime;
                        if (_timer >= 1f)
                        {
                            _timer = 0f;
                            SendNextMessage();
                        }
                    }
                    else
                    {
                        // It's the Player's turn. Show the button, wait for click.
                        SetVisualsActive(true);
                        _timer = 0f;
                    }
                }
                else
                {
                    // All dialogues in this node have been sent and completed animating
                    CompleteCurrentNode(currentContact, currentNode);
                }
            }
            else
            {
                SetVisualsActive(false);
                if (_button != null) _button.gameObject.SetActive(false);
                currentContact.SetHasNewMessages(false);
            }
        }
        else
        {
            SetVisualsActive(false);
            if (_button != null) _button.gameObject.SetActive(false);
        }
    }

    private void ShowNarration(Contact currentContact, Message narration)
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[DialogueButton] Cannot show narration because DialogueManager is unavailable.");
            return;
        }

        _narrationDialogueActive = true;
        bool opened = DialogueManager.Instance.ShowNarration(
            narration.Content(),
            () =>
            {
                _narrationDialogueActive = false;
                _timer = 0f;
            });

        if (!opened)
        {
            _narrationDialogueActive = false;
            return;
        }

        // Narration is consumed by the dialogue window, not the phone history.
        // Advancing here lets the normal Update flow continue with any line that follows.
        currentContact.SetCurrentMessageIndex(currentContact.CurrentMessageIndex() + 1);
    }

    private void OnDialogueButtonPressed()
    {
        if (_button != null && _button.interactable)
        {
            SendNextMessage();
        }
    }

    private void SendNextMessage()
    {
        if (MessagesManager.Instance == null)
            return;

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact != null && currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (currentNode != null)
            {
                List<Message> activeDialogues = currentNode.GetActiveDialogues();
                int msgIndex = currentContact.CurrentMessageIndex();
                if (msgIndex < activeDialogues.Count)
                {
                    Message msg = activeDialogues[msgIndex];
                    MessagesManager.Instance.SendMessageToCurrentContact(msg);
                    currentContact.SetCurrentMessageIndex(msgIndex + 1);
                }
            }
        }
    }

    private void CompleteCurrentNode(Contact currentContact, NarrativeNode currentNode)
    {
        currentContact.SetHasNewMessages(false);
        if (_button != null) _button.gameObject.SetActive(false);
        SetVisualsActive(false);

        if (!currentNode.HasOptions())
        {
            currentNode.ApplyEndReputationImpact();

            if (!string.IsNullOrEmpty(currentNode.EventOnEnd()) && EventBus.Instance != null)
            {
                EventBus.Instance.Publish(currentNode.EventOnEnd());
            }

            int nextNodeId = currentNode.IsLoop() ? currentNode.GetLoopTargetNodeId() : currentNode.ResolveNextNode();
            nextNodeId = MessagesManager.Instance.ResolveEntryRedirects(nextNodeId);

            currentContact.SetNarrativeNode(nextNodeId);
            currentContact.SetCurrentMessageIndex(0);

            if (nextNodeId < 0)
            {
                return;
            }

            NarrativeNode next = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (next != null)
            {
                if (next.GetActiveDialogues().Count > 0)
                {
                    currentContact.SetHasNewMessages(true);
                    gameObject.SetActive(true);
                    MessagesManager.Instance.GetPhoneStateMachine()?.ShowDialogueTab();
                }
                else if (next.HasOptions())
                {
                    MessagesManager.Instance.GetOptionsChanger()?.SetCurrentNode(next);
                    MessagesManager.Instance.GetOptionsChanger()?.ActiveDecisionsPanel(true);
                    MessagesManager.Instance.GetPhoneStateMachine()?.ShowDecisionsTab();
                }
            }
        }
        else
        {
            // Show options panel
            MessagesManager.Instance.GetOptionsChanger()?.SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger()?.ActiveDecisionsPanel(true);
            MessagesManager.Instance.GetPhoneStateMachine()?.ShowDecisionsTab();
        }
    }
}
