using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MessagesUI : MonoBehaviour
{
    [Header("UI Containers")]
    [SerializeField] private Transform contactListContainer;
    [SerializeField] private Transform messagesContainer;

    [Header("Prefabs")]
    [SerializeField] private GameObject messagePrefab;
    [SerializeField] private GameObject contactPrefab;

    [Header("Messages Colors")]
    [SerializeField] private Color playerMessageColor;
    [SerializeField] private Color NPCMessageColor;

    [Header("Scroll")]
    [SerializeField] private ScrollRect messagesScrollRect;

    [Header("Contact Info Header")]
    [SerializeField] private TextMeshProUGUI contactNameHeader;

    [Header("Animation Settings")]
    [SerializeField] private float npcTypingDuration = 2.0f;
    [SerializeField] private float npcDotInterval = 0.35f;
    [SerializeField] private float playerTypewriterSpeed = 0.03f;

    private readonly Queue<Message> _pendingMessages = new Queue<Message>();
    private Coroutine _processQueueCoroutine;
    private Coroutine _scrollCoroutine;
    private bool _isAnimating = false;

    public bool IsAnimating => _isAnimating || _pendingMessages.Count > 0;

    private void Awake()
    {
        EnsureScrollRect();
        EnsureContactNameHeader();
    }

    void Start()
    {
        EnsureScrollRect();
        EnsureContactNameHeader();
        CreateContacts(); 
    }

    private void OnDisable()
    {
        StopAnimationQueue();
        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
            _scrollCoroutine = null;
        }
    }

    private void EnsureScrollRect()
    {
        if (messagesScrollRect == null && messagesContainer != null)
        {
            messagesScrollRect = messagesContainer.GetComponentInParent<ScrollRect>();
        }
    }

    private void EnsureContactNameHeader()
    {
        if (contactNameHeader != null) return;

        // 1. Search up the parent chain of messagesContainer to find ContactInfo/ContactName
        if (messagesContainer != null)
        {
            Transform current = messagesContainer;
            while (current != null)
            {
                Transform contactInfo = current.Find("ContactInfo");
                if (contactInfo != null)
                {
                    Transform contactName = contactInfo.Find("ContactName");
                    if (contactName != null)
                    {
                        contactNameHeader = contactName.GetComponent<TextMeshProUGUI>();
                        if (contactNameHeader != null) return;
                    }
                }
                current = current.parent;
            }
        }

        // 2. Fallback search across scene objects
        if (contactNameHeader == null)
        {
            TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            foreach (var t in allTexts)
            {
                if (t.gameObject.scene.isLoaded && t.gameObject.name == "ContactName" && t.transform.parent != null && t.transform.parent.name == "ContactInfo")
                {
                    contactNameHeader = t;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Set the contact name text in ContactInfo/ContactName
    /// </summary>
    public void SetContactName(string contactName)
    {
        EnsureContactNameHeader();
        if (contactNameHeader != null && !string.IsNullOrEmpty(contactName))
        {
            contactNameHeader.text = contactName;
        }
    }

    /// <summary>
    /// Update the contact name header using the current contact from MessagesManager
    /// </summary>
    public void UpdateContactNameHeader(string contactName = null)
    {
        EnsureContactNameHeader();
        if (string.IsNullOrEmpty(contactName) && MessagesManager.Instance != null)
        {
            contactName = MessagesManager.Instance.GetCurrentContactName();
        }

        if (contactNameHeader != null && !string.IsNullOrEmpty(contactName))
        {
            contactNameHeader.text = contactName;
        }
    }

    public void AddContact(Contact contact)
    {
        InstantiateContact(contact);
    }

    /// <summary>
    /// Refresh the contacts list in the UI
    /// </summary>
    public void RefreshContacts()
    {
        if (contactListContainer == null) return;

        for (int i = contactListContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(contactListContainer.GetChild(i).gameObject);
        }
        CreateContacts();
    }

    /// <summary>
    /// Create UI elements for each contact in the contact list container.
    /// </summary>
    private void CreateContacts()
    {
        if (MessagesManager.Instance == null || contactListContainer == null) return;

        List<Contact> contacts = MessagesManager.Instance.GetContacts();
        if (contacts != null && contacts.Count > 0)
        {
            foreach (Contact contact in contacts)
            {
                if (MessagesManager.Instance.ContactHasMessages(contact.Name()))
                {
                    InstantiateContact(contact);
                }
            }
        }
    }

    private void InstantiateContact(Contact contact)
    {
        GameObject contactGO = Instantiate(contactPrefab, contactListContainer);
        TextMeshProUGUI contactNameText = contactGO.GetComponentInChildren<TextMeshProUGUI>();
        if (contactNameText != null)
        {
            contactNameText.text = contact.Name();
        }
    }

    /// <summary>
    /// Create all messages related to the current contact (historical load without animation)
    /// </summary>
    public void CreateMessages()
    {
        StopAnimationQueue();
        UpdateContactNameHeader();

        List<Message> currentContactMessages = MessagesManager.Instance.GetCurrentContactMessages();
        ClearMessages();
        if (currentContactMessages != null && currentContactMessages.Count > 0)
        {
            float availableWidth = GetAvailableWidth();
            foreach (Message message in currentContactMessages)
            {
                InstantiateHistoricalMessage(message, availableWidth);
            }

            RefreshLayoutAndScroll();
        }
        else
        {
            Debug.LogError("No messages found for the current contact.");
        }
    }

    /// <summary>
    /// Destroy all message UI elements in the messages container.
    /// </summary>
    private void ClearMessages()
    {
        for (int i = messagesContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(messagesContainer.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Add a newly sent message to the conversation with typing animations and autoscroll.
    /// </summary>
    public void AddMessage(Message message) 
    {
        _pendingMessages.Enqueue(message);
        if (_processQueueCoroutine == null && gameObject.activeInHierarchy)
        {
            _processQueueCoroutine = StartCoroutine(ProcessMessageQueue());
        }
    }

    private void StopAnimationQueue()
    {
        if (_processQueueCoroutine != null)
        {
            StopCoroutine(_processQueueCoroutine);
            _processQueueCoroutine = null;
        }
        _pendingMessages.Clear();
        _isAnimating = false;
    }

    private IEnumerator ProcessMessageQueue()
    {
        _isAnimating = true;
        while (_pendingMessages.Count > 0)
        {
            Message msg = _pendingMessages.Dequeue();
            yield return StartCoroutine(AnimateMessage(msg));
        }
        _isAnimating = false;
        _processQueueCoroutine = null;
    }

    private void InstantiateHistoricalMessage(Message message, float availableWidth)
    {
        GameObject messageGO = Instantiate(messagePrefab, messagesContainer);
        CleanUpConflictingComponents(messageGO);

        TextMeshProUGUI messageText = messageGO.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message.Content();
        }

        ApplyMessageDimensions(messageGO, message.Content(), message.Sender(), availableWidth);
    }

    private IEnumerator AnimateMessage(Message message)
    {
        GameObject messageGO = Instantiate(messagePrefab, messagesContainer);
        CleanUpConflictingComponents(messageGO);

        float availableWidth = GetAvailableWidth();
        TextMeshProUGUI messageText = messageGO.GetComponentInChildren<TextMeshProUGUI>();
        string fullContent = message.Content() ?? "";

        if (message.Sender() == Sender.NPC)
        {
            // NPC typing animation:
            // Display '.' -> '..' -> '...' for 2 seconds before showing the full message.
            ApplyMessageDimensions(messageGO, "...", message.Sender(), availableWidth);
            if (messageText != null)
            {
                messageText.text = ".";
            }
            RefreshLayoutAndScroll();

            float elapsed = 0f;
            float dotTimer = 0f;
            int dotCount = 1;

            while (elapsed < npcTypingDuration)
            {
                if (WasSkipPressed())
                {
                    break;
                }

                elapsed += Time.deltaTime;
                dotTimer += Time.deltaTime;

                if (dotTimer >= npcDotInterval)
                {
                    dotTimer = 0f;
                    dotCount = (dotCount % 3) + 1; // 1 -> 2 -> 3 -> 1 ...
                    if (messageText != null)
                    {
                        messageText.text = new string('.', dotCount);
                    }
                }

                yield return null;
            }

            // Reveal complete message and resize bubble
            if (messageText != null)
            {
                messageText.text = fullContent;
            }
            ApplyMessageDimensions(messageGO, fullContent, message.Sender(), availableWidth);
            RefreshLayoutAndScroll();
        }
        else
        {
            // Player message:
            // Typewriter effect letter by letter. Skip to full text if 'E' (_pInteractAction) is pressed.
            ApplyMessageDimensions(messageGO, fullContent, message.Sender(), availableWidth);
            RefreshLayoutAndScroll();

            if (messageText != null)
            {
                messageText.text = fullContent;
                messageText.ForceMeshUpdate();

                int totalCharacters = messageText.textInfo.characterCount;
                messageText.maxVisibleCharacters = 0;

                float charTimer = 0f;
                int visibleChars = 0;

                while (visibleChars < totalCharacters)
                {
                    if (WasSkipPressed())
                    {
                        messageText.maxVisibleCharacters = totalCharacters;
                        break;
                    }

                    charTimer += Time.deltaTime;
                    while (charTimer >= playerTypewriterSpeed && visibleChars < totalCharacters)
                    {
                        charTimer -= playerTypewriterSpeed;
                        visibleChars++;
                        messageText.maxVisibleCharacters = visibleChars;
                    }

                    yield return null;
                }

                messageText.maxVisibleCharacters = totalCharacters;
            }

            RefreshLayoutAndScroll();
        }
    }

    private void ApplyMessageDimensions(GameObject messageGO, string textForMeasurement, Sender sender, float availableWidth)
    {
        if (messageGO == null) return;

        // Message bubbles occupy a dynamic width up to 80% maximum of available width
        float maxBubbleWidth = availableWidth * 0.8f;
        float horizontalMargin = 10f;
        float verticalMargin = 8f;
        float maxTextWidth = maxBubbleWidth - (horizontalMargin * 2f);
        float minBubbleWidth = 32f;

        TextMeshProUGUI messageText = messageGO.GetComponentInChildren<TextMeshProUGUI>();
        Image image = messageGO.GetComponentInChildren<Image>();
        RectTransform messageRect = messageGO.GetComponent<RectTransform>();

        float textHeight = 18f;
        float bubbleWidth = maxBubbleWidth;

        if (messageText != null)
        {
            messageText.textWrappingMode = TextWrappingModes.Normal;
            Vector2 textSize = messageText.GetPreferredValues(textForMeasurement, maxTextWidth, 0f);

            // Add a small 2px buffer to avoid accidental wrapping due to subpixel glyph rounding
            float desiredTextWidth = Mathf.Min(textSize.x + 2f, maxTextWidth);
            bubbleWidth = Mathf.Clamp(desiredTextWidth + (horizontalMargin * 2f), minBubbleWidth, maxBubbleWidth);
            textHeight = Mathf.Max(18f, textSize.y);

            // Anchor text to fill bubble with margins
            RectTransform textRect = messageText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(horizontalMargin, verticalMargin);
            textRect.offsetMax = new Vector2(-horizontalMargin, -verticalMargin);
        }

        float bubbleHeight = Mathf.Max(28f, textHeight + (verticalMargin * 2f));

        // Position bubble inside the row:
        // Player has xOffset = availableWidth - bubbleWidth (pinned right, >= 20% empty on left).
        // NPC has xOffset = 0 (pinned left, >= 20% empty on right).
        if (image != null)
        {
            RectTransform bubbleRect = image.rectTransform;
            bubbleRect.anchorMin = new Vector2(0f, 1f);
            bubbleRect.anchorMax = new Vector2(0f, 1f);
            bubbleRect.pivot = new Vector2(0f, 1f);
            bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);

            bool isPlayer = (sender == Sender.Player);
            if (isPlayer)
            {
                float xOffset = availableWidth - bubbleWidth;
                bubbleRect.anchoredPosition = new Vector2(xOffset, 0f);
                image.color = playerMessageColor;
            }
            else
            {
                bubbleRect.anchoredPosition = Vector2.zero;
                image.color = NPCMessageColor;
            }
        }

        // Configure message row size and layout element for VerticalLayoutGroup
        if (messageRect != null)
        {
            messageRect.anchorMin = new Vector2(0f, 1f);
            messageRect.anchorMax = new Vector2(0f, 1f);
            messageRect.pivot = new Vector2(0f, 1f);
            messageRect.anchoredPosition = Vector2.zero;
            messageRect.sizeDelta = new Vector2(availableWidth, bubbleHeight);
        }

        LayoutElement rowLayout = messageGO.GetComponent<LayoutElement>();
        if (rowLayout == null)
        {
            rowLayout = messageGO.AddComponent<LayoutElement>();
        }
        rowLayout.flexibleWidth = 1f;
        rowLayout.minWidth = availableWidth;
        rowLayout.preferredWidth = availableWidth;
        rowLayout.minHeight = bubbleHeight;
        rowLayout.preferredHeight = bubbleHeight;
        rowLayout.flexibleHeight = 0f;
    }

    private void CleanUpConflictingComponents(GameObject messageGO)
    {
        if (messageGO == null) return;

        HorizontalLayoutGroup oldHlg = messageGO.GetComponent<HorizontalLayoutGroup>();
        if (oldHlg != null) DestroyImmediate(oldHlg);

        Image image = messageGO.GetComponentInChildren<Image>();
        if (image != null)
        {
            VerticalLayoutGroup bubbleVlg = image.GetComponent<VerticalLayoutGroup>();
            if (bubbleVlg != null) DestroyImmediate(bubbleVlg);
            LayoutElement bubbleLe = image.GetComponent<LayoutElement>();
            if (bubbleLe != null) DestroyImmediate(bubbleLe);
        }

        TextMeshProUGUI messageText = messageGO.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            ContentSizeFitter csf = messageText.GetComponent<ContentSizeFitter>();
            if (csf != null) DestroyImmediate(csf);
        }
    }

    private float GetAvailableWidth()
    {
        RectTransform containerRect = messagesContainer as RectTransform;
        float containerWidth = (containerRect != null && containerRect.rect.width > 50f)
            ? containerRect.rect.width
            : 183f;

        float availableWidth = containerWidth;
        VerticalLayoutGroup parentVlg = messagesContainer != null ? messagesContainer.GetComponent<VerticalLayoutGroup>() : null;
        if (parentVlg != null)
        {
            availableWidth = containerWidth - (parentVlg.padding.left + parentVlg.padding.right);
        }
        return availableWidth;
    }

    public void RefreshLayoutAndScroll()
    {
        EnsureScrollRect();
        if (messagesContainer is RectTransform rt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
        ScrollToBottom();
    }

    public void ScrollToBottom()
    {
        if (gameObject.activeInHierarchy)
        {
            if (_scrollCoroutine != null)
            {
                StopCoroutine(_scrollCoroutine);
            }
            _scrollCoroutine = StartCoroutine(ScrollToBottomCoroutine());
        }
    }

    private IEnumerator ScrollToBottomCoroutine()
    {
        yield return new WaitForEndOfFrame();
        EnsureScrollRect();

        if (messagesContainer is RectTransform rt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        if (messagesScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            messagesScrollRect.velocity = Vector2.zero;
            messagesScrollRect.verticalNormalizedPosition = 0f;
        }

        yield return null;

        if (messagesScrollRect != null)
        {
            messagesScrollRect.velocity = Vector2.zero;
            messagesScrollRect.verticalNormalizedPosition = 0f;
        }
        _scrollCoroutine = null;
    }

    private bool WasSkipPressed()
    {
        try
        {
            if (InputManager.Instance != null && InputManager.Instance.InteractWasPressed())
            {
                return true;
            }
        }
        catch { }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Legacy compatibility method
    /// </summary>
    public void InstantiateMessage(Message message)
    {
        AddMessage(message);
    }
}
