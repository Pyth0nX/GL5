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
    [SerializeField] private GameObject imageMessagePrefab;
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

    private static readonly Dictionary<string, Sprite> _loadedSpritesCache = new Dictionary<string, Sprite>();

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
        if (message.HasImage())
        {
            GameObject prefabToUse = (imageMessagePrefab != null) ? imageMessagePrefab : messagePrefab;
            GameObject messageGO = Instantiate(prefabToUse, messagesContainer);
            CleanUpConflictingComponents(messageGO);

            Sprite sprite = LoadSprite(message.ImagePath());
            ApplyImageMessageDimensions(messageGO, message, sprite, availableWidth, showPhoto: true);
        }
        else
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
    }

    private IEnumerator AnimateMessage(Message message)
    {
        float availableWidth = GetAvailableWidth();

        if (message.HasImage())
        {
            GameObject prefabToUse = (imageMessagePrefab != null) ? imageMessagePrefab : messagePrefab;
            GameObject messageGO = Instantiate(prefabToUse, messagesContainer);
            CleanUpConflictingComponents(messageGO);

            Sprite sprite = LoadSprite(message.ImagePath());

            if (message.Sender() == Sender.NPC)
            {
                // 1. Create the image message upfront with photo HIDDEN and bubble in compact typing size (48x28)
                ApplyImageMessageDimensions(messageGO, message, sprite, availableWidth, showPhoto: false);
                RefreshLayoutAndScroll();

                TextMeshProUGUI tempText = messageGO.GetComponentInChildren<TextMeshProUGUI>(true);

                float elapsed = 0f;
                float dotTimer = 0f;
                int dotCount = 1;

                while (elapsed < npcTypingDuration)
                {
                    if (WasSkipPressed()) break;

                    elapsed += Time.deltaTime;
                    dotTimer += Time.deltaTime;
                    if (dotTimer >= npcDotInterval)
                    {
                        dotTimer = 0f;
                        dotCount = (dotCount % 3) + 1;
                        if (tempText != null)
                        {
                            tempText.text = new string('.', dotCount);
                        }
                    }
                    yield return null;
                }

                // 2. Typing animation finished: FIRST ACTIVATE THE IMAGE (SetActive(true) and alpha = 1)
                Image photoImage = null;
                Image[] allImages = messageGO.GetComponentsInChildren<Image>(true);
                foreach (var img in allImages)
                {
                    if (img.gameObject.name == "Photo" || img.gameObject.name == "ImageContent")
                    {
                        photoImage = img;
                        break;
                    }
                }
                if (photoImage != null)
                {
                    photoImage.gameObject.SetActive(true);
                    CanvasGroup cg = photoImage.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                }

                // 3. RESIZE BUBBLE RESPECT TO IMAGE SIZE AFTER ACTIVATING IT
                ApplyImageMessageDimensions(messageGO, message, sprite, availableWidth, showPhoto: true);
                RefreshLayoutAndScroll();
            }
            else
            {
                // Player sent image: immediately visible on the right
                ApplyImageMessageDimensions(messageGO, message, sprite, availableWidth, showPhoto: true);
                RefreshLayoutAndScroll();
                yield return new WaitForSeconds(0.1f);
            }
        }
        else
        {
            GameObject messageGO = Instantiate(messagePrefab, messagesContainer);
            CleanUpConflictingComponents(messageGO);

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

    private void ApplyImageMessageDimensions(GameObject messageGO, Message message, Sprite sprite, float availableWidth, bool showPhoto = true)
    {
        if (messageGO == null) return;

        float maxBubbleWidth = availableWidth * 0.8f;
        float padding = 8f;
        float minBubbleWidth = 40f;

        // 1. Locate components
        Image bubbleImage = null;
        Image[] allImages = messageGO.GetComponentsInChildren<Image>(true);
        Image photoImage = null;

        foreach (var img in allImages)
        {
            if (img.gameObject.name == "Photo" || img.gameObject.name == "ImageContent")
            {
                photoImage = img;
            }
            else if (img.gameObject.name == "Bubble" || (bubbleImage == null && img.gameObject != messageGO))
            {
                bubbleImage = img;
            }
        }

        if (bubbleImage == null && allImages.Length > 0)
        {
            bubbleImage = allImages[0];
        }

        // Create Photo under Bubble if it doesn't exist yet
        if (photoImage == null && bubbleImage != null)
        {
            GameObject photoGO = new GameObject("Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            photoGO.transform.SetParent(bubbleImage.transform, false);
            photoImage = photoGO.GetComponent<Image>();
        }

        CanvasGroup photoCg = null;
        if (photoImage != null)
        {
            photoCg = photoImage.GetComponent<CanvasGroup>();
            if (photoCg == null)
            {
                photoCg = photoImage.gameObject.AddComponent<CanvasGroup>();
            }

            photoImage.sprite = sprite;
            photoImage.type = Image.Type.Simple;
            photoImage.preserveAspect = true;
            photoImage.color = Color.white;
            photoImage.raycastTarget = true;

            // Apply visibility: hide with SetActive(false) and alpha = 0, or show with SetActive(true) and alpha = 1
            photoImage.gameObject.SetActive(showPhoto);
            photoCg.alpha = showPhoto ? 1f : 0f;
        }

        TextMeshProUGUI captionText = messageGO.GetComponentInChildren<TextMeshProUGUI>(true);
        string caption = message.Content();
        bool hasCaption = !string.IsNullOrEmpty(caption);

        // 2. Compute dimensions
        float bubbleWidth;
        float bubbleHeight;

        if (showPhoto)
        {
            // Sized to the actual image + caption
            float photoWidth = maxBubbleWidth - (padding * 2f);
            float photoHeight = photoWidth;

            if (sprite != null && sprite.rect.width > 0f)
            {
                float aspect = sprite.rect.height / sprite.rect.width;
                photoHeight = photoWidth * aspect;

                float maxPhotoHeight = 220f;
                if (photoHeight > maxPhotoHeight)
                {
                    photoHeight = maxPhotoHeight;
                    photoWidth = photoHeight / aspect;
                }
            }
            else
            {
                photoHeight = 100f;
            }

            bubbleWidth = Mathf.Clamp(photoWidth + (padding * 2f), minBubbleWidth, maxBubbleWidth);
            bubbleHeight = photoHeight + (padding * 2f);

            // Configure Photo RectTransform
            if (photoImage != null)
            {
                RectTransform photoRect = photoImage.rectTransform;
                photoRect.anchorMin = new Vector2(0f, 1f);
                photoRect.anchorMax = new Vector2(0f, 1f);
                photoRect.pivot = new Vector2(0f, 1f);
                photoRect.anchoredPosition = new Vector2((bubbleWidth - photoWidth) * 0.5f, -padding);
                photoRect.sizeDelta = new Vector2(photoWidth, photoHeight);
            }

            // Configure Caption
            if (captionText != null)
            {
                if (hasCaption)
                {
                    captionText.gameObject.SetActive(true);
                    captionText.text = caption;
                    captionText.textWrappingMode = TextWrappingModes.Normal;
                    captionText.alignment = TextAlignmentOptions.TopLeft;

                    Vector2 textSize = captionText.GetPreferredValues(caption, photoWidth, 0f);
                    float captionHeight = Mathf.Max(18f, textSize.y);

                    RectTransform captionRect = captionText.rectTransform;
                    captionRect.anchorMin = new Vector2(0f, 1f);
                    captionRect.anchorMax = new Vector2(0f, 1f);
                    captionRect.pivot = new Vector2(0f, 1f);
                    captionRect.anchoredPosition = new Vector2((bubbleWidth - photoWidth) * 0.5f, -(padding + photoHeight + 4f));
                    captionRect.sizeDelta = new Vector2(photoWidth, captionHeight);

                    bubbleHeight += captionHeight + 4f;
                }
                else
                {
                    captionText.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // Typing state: compact bubble sized for typing dots ("...")
            bubbleWidth = 48f;
            bubbleHeight = 28f;

            if (captionText != null)
            {
                captionText.gameObject.SetActive(true);
                captionText.text = ".";
                captionText.alignment = TextAlignmentOptions.Center;

                RectTransform captionRect = captionText.rectTransform;
                captionRect.anchorMin = Vector2.zero;
                captionRect.anchorMax = Vector2.one;
                captionRect.pivot = new Vector2(0.5f, 0.5f);
                captionRect.anchoredPosition = Vector2.zero;
                captionRect.offsetMin = new Vector2(4f, 4f);
                captionRect.offsetMax = new Vector2(-4f, -4f);
            }
        }

        // 3. Position Bubble inside the row
        if (bubbleImage != null)
        {
            RectTransform bubbleRect = bubbleImage.rectTransform;
            bubbleRect.anchorMin = new Vector2(0f, 1f);
            bubbleRect.anchorMax = new Vector2(0f, 1f);
            bubbleRect.pivot = new Vector2(0f, 1f);
            bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);

            bool isPlayer = (message.Sender() == Sender.Player);
            if (isPlayer)
            {
                float xOffset = availableWidth - bubbleWidth;
                bubbleRect.anchoredPosition = new Vector2(xOffset, 0f);
                bubbleImage.color = playerMessageColor;
            }
            else
            {
                bubbleRect.anchoredPosition = Vector2.zero;
                bubbleImage.color = NPCMessageColor;
            }
        }

        // 4. Configure Message Row LayoutElement
        RectTransform messageRect = messageGO.GetComponent<RectTransform>();
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

    /// <summary>
    /// Loads a sprite from Resources, StreamingAssets, or project-relative file paths with caching.
    /// </summary>
    public static Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (_loadedSpritesCache.TryGetValue(path, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite loadedSprite = null;

        // 1. Try Resources.Load directly
        loadedSprite = Resources.Load<Sprite>(path);

        // 2. Try Resources.Load without file extension
        if (loadedSprite == null)
        {
            string ext = System.IO.Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
            {
                string dir = System.IO.Path.GetDirectoryName(path) ?? "";
                string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(path);
                string pathNoExt = System.IO.Path.Combine(dir, nameWithoutExt).Replace('\\', '/');
                loadedSprite = Resources.Load<Sprite>(pathNoExt);
            }
        }

        // 3. Try File System (StreamingAssets, project-relative, dataPath, or absolute)
        if (loadedSprite == null)
        {
            string fullPath = path;

            if (!System.IO.File.Exists(fullPath))
            {
                // StreamingAssets
                string saPath = System.IO.Path.Combine(Application.streamingAssetsPath, path);
                if (System.IO.File.Exists(saPath))
                {
                    fullPath = saPath;
                }
                else
                {
                    // StreamingAssets/Images subfolder fallback
                    string saImgPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Images", path);
                    if (System.IO.File.Exists(saImgPath))
                    {
                        fullPath = saImgPath;
                    }
                    else
                    {
                        // Project root relative
                        string projPath = System.IO.Path.Combine(Application.dataPath, "..", path);
                        if (System.IO.File.Exists(projPath))
                        {
                            fullPath = projPath;
                        }
                        else
                        {
                            // Assets relative
                            string inDataPath = System.IO.Path.Combine(Application.dataPath, path);
                            if (System.IO.File.Exists(inDataPath))
                            {
                                fullPath = inDataPath;
                            }
                        }
                    }
                }
            }

            if (System.IO.File.Exists(fullPath))
            {
                try
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.name = System.IO.Path.GetFileNameWithoutExtension(fullPath);
                        loadedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MessagesUI] Error loading image from '{fullPath}': {ex.Message}");
                }
            }
        }

        if (loadedSprite != null)
        {
            _loadedSpritesCache[path] = loadedSprite;
        }
        else
        {
            Debug.LogWarning($"[MessagesUI] Image not found at path '{path}'.");
        }

        return loadedSprite;
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
