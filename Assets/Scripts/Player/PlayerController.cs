using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Player Name")]
    [SerializeField] private string _playerName = "Luna";

    [Tooltip("Movement Speed")]
    [SerializeField] private float _movementSpeed = 5f;
    [Tooltip("Mouse Sensitivity")]
    [SerializeField] private float _mouseSensitivity = 1f;

    [Tooltip("Interact Distance")]
    [SerializeField] private float _interactDistance = 5f;

    [Tooltip("Vertical Clamp Limits: 0° = Look down, 90° = Look up!")]
    [Range(0f, 90f)]
    [SerializeField] private float _verticalClamp = 80f;

    // Scales raw pixel mouse delta so default sensitivity (~1.75) feels natural without clamping input.
    private const float MouseDeltaScale = 0.1f;

    private IInteractable _currentHoveredInteractable;
    private InteractableOutline _currentHoveredStandaloneOutline;
    private bool _isShowingHoverMessage;
    private bool _lastHoveredCanInteract;
    private string _lastHoveredMessage;
    private PlayerInput _playerInput;
    private PlayerStateMachine _playerStateMachine;
    private Camera _playerCamera;
    private Rigidbody _rigidbody;
    private readonly RaycastHit[] _raycastHits = new RaycastHit[32];
    private float _currentXRotation;
    private Coroutine _postTeleportSyncCoroutine;

    private void Awake()
    {
        Instance = this;
        _playerCamera = GetComponentInChildren<Camera>();
        _playerInput = GetComponent<PlayerInput>();
        _playerStateMachine = GetComponent<PlayerStateMachine>();
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody != null)
        {
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        _currentXRotation = 0f;
        if (GameManager.Instance != null && string.IsNullOrWhiteSpace(GameManager.Instance.GetPlayerName()))
        {
            GameManager.Instance.SetPlayerName(_playerName);
        }

        if (SettingsManager.Instance != null)
        {
            _mouseSensitivity = SettingsManager.Instance.CameraSensitivity;
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("Conversation_Ended", OnConversationEnded);
            EventBus.Instance.Subscribe<float>(SettingsManager.EventCameraSensitivityChanged, SetMouseSensitivity);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("Conversation_Ended", OnConversationEnded);
            EventBus.Instance.Unsubscribe<float>(SettingsManager.EventCameraSensitivityChanged, SetMouseSensitivity);
        }
    }

    private void OnConversationEnded()
    {
        InputManager.Instance?.SuppressInteract();
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() == PlayerState.Interacting)
        {
            _playerStateMachine.ChangeState(PlayerState.Idle);
        }
    }

    private void Update()
    {
        if ((DebugCampaignPanelUI.Instance != null && DebugCampaignPanelUI.Instance.IsOpen) ||
            (UIManager.Instance != null && UIManager.Instance.IsScenarioSystemOpen) ||
            (ScenarioSystemUI.Instance != null && ScenarioSystemUI.Instance.IsVisible) ||
            (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning))
        {
            ClearHoveredInteractable();
            return;
        }

        LookAround();

        CheckInteractableHover();

        if (InputManager.Instance != null && InputManager.Instance.InteractWasPressed())
        {
            TryInteract();
        }

        if (InputManager.Instance != null && InputManager.Instance.PhoneWasPressed())
        {
            HandlePhone();
        }
    }

    private void FixedUpdate()
    {
        if ((DebugCampaignPanelUI.Instance != null && DebugCampaignPanelUI.Instance.IsOpen) ||
            (UIManager.Instance != null && UIManager.Instance.IsScenarioSystemOpen) ||
            (ScenarioSystemUI.Instance != null && ScenarioSystemUI.Instance.IsVisible) ||
            (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning))
        {
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
            }
            return;
        }

        MovePlayer();
    }

    private void HandlePhone()
    {
        if ((UIManager.Instance != null && UIManager.Instance.IsScenarioSystemOpen) ||
            (ScenarioSystemUI.Instance != null && ScenarioSystemUI.Instance.IsVisible) ||
            (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning))
        {
            return;
        }

        _playerStateMachine.SetPhoneOpen(!_playerStateMachine.IsPhoneOpen());
        if (_playerStateMachine.IsPhoneOpen())
        {
            ClearHoveredInteractable();
            _playerStateMachine.ChangeState(PlayerState.Phone);
            UIManager.Instance.ChangeState(1);
        }
        else
        {
            _playerStateMachine.ChangeState(PlayerState.Idle);
            UIManager.Instance.ChangeState(0);
        }
    }

    private void OnDisable()
    {
        ClearHoveredInteractable();
    }

    /// <summary>
    /// Immediately halts player velocity and clears any active hover outlines or interaction prompts.
    /// </summary>
    public void StopMovementImmediately()
    {
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        ClearHoveredInteractable();
    }

    /// <summary>
    /// Teleports the player to a target position and rotation, immediately resetting velocity,
    /// rigidbody interpolation state, vertical camera pitch, and syncing physics transforms.
    /// </summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        StopMovementImmediately();

        // Split spawner orientation into upright horizontal yaw for the capsule and clamped pitch for the camera
        Vector3 euler = rotation.eulerAngles;
        Quaternion bodyRotation = Quaternion.Euler(0f, euler.y, 0f);
        float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -_verticalClamp, _verticalClamp);

        RigidbodyInterpolation previousInterpolation = RigidbodyInterpolation.None;
        if (_rigidbody != null)
        {
            previousInterpolation = _rigidbody.interpolation;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
        }

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, bodyRotation);
            characterController.enabled = true;
        }
        else
        {
            transform.SetPositionAndRotation(position, bodyRotation);
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.position = position;
            _rigidbody.rotation = bodyRotation;
        }

        _currentXRotation = pitch;
        if (_playerCamera != null)
        {
            _playerCamera.transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
        }

        Physics.SyncTransforms();

        if (_rigidbody != null)
        {
            if (gameObject.activeInHierarchy)
            {
                if (_postTeleportSyncCoroutine != null)
                {
                    StopCoroutine(_postTeleportSyncCoroutine);
                }
                _postTeleportSyncCoroutine = StartCoroutine(RestoreRigidbodyAfterTeleport(position, bodyRotation, previousInterpolation));
            }
            else
            {
                _rigidbody.interpolation = previousInterpolation;
            }
        }
    }

    private IEnumerator RestoreRigidbodyAfterTeleport(Vector3 position, Quaternion bodyRotation, RigidbodyInterpolation previousInterpolation)
    {
        yield return new WaitForFixedUpdate();

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.position = position;
            _rigidbody.rotation = bodyRotation;
            transform.SetPositionAndRotation(position, bodyRotation);
            Physics.SyncTransforms();
            _rigidbody.interpolation = previousInterpolation;
        }

        _postTeleportSyncCoroutine = null;
    }

    /// <summary>
    /// Teleports the player to a target transform's position and rotation.
    /// </summary>
    public void Teleport(Transform targetTransform)
    {
        if (targetTransform == null) return;
        Teleport(targetTransform.position, targetTransform.rotation);
    }

    private void ClearHoveredInteractable()
    {
        if (_currentHoveredInteractable != null)
        {
            _currentHoveredInteractable.SetOutlineActive(false);
            _currentHoveredInteractable = null;
        }

        if (_currentHoveredStandaloneOutline != null)
        {
            _currentHoveredStandaloneOutline.SetOutlineActive(false);
            _currentHoveredStandaloneOutline = null;
        }

        _lastHoveredCanInteract = false;
        _lastHoveredMessage = null;

        if (_isShowingHoverMessage)
        {
            _isShowingHoverMessage = false;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideMessage();
            }
        }
    }

    private void MovePlayer()
    {
        if ((DebugCampaignPanelUI.Instance != null && DebugCampaignPanelUI.Instance.IsOpen) ||
            (UIManager.Instance != null && UIManager.Instance.IsScenarioSystemOpen) ||
            (ScenarioSystemUI.Instance != null && ScenarioSystemUI.Instance.IsVisible))
        {
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
            }
            return;
        }

        if (InputManager.Instance == null) return;

        Vector2 direction = InputManager.Instance.GetPlayerMovement();

        Vector3 moveDirection = transform.right * direction.x + transform.forward * direction.y;
        moveDirection.y = 0f;

        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        Vector3 targetVelocity = moveDirection * _movementSpeed;
        targetVelocity.y = _rigidbody.linearVelocity.y;

        _rigidbody.linearVelocity = targetVelocity;
    }

    private void LookAround()
    {
        if (_playerCamera == null || Cursor.lockState != CursorLockMode.Locked) return;

        if (InputManager.Instance != null && InputManager.Instance.PlayerLooked())
        {
            Vector2 lookVec = InputManager.Instance.GetPlayerLook();
            float sensitivity = _mouseSensitivity * MouseDeltaScale;

            transform.Rotate(0f, lookVec.x * sensitivity, 0f, Space.World);
            if (_rigidbody != null)
            {
                _rigidbody.rotation = transform.rotation;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            _currentXRotation = Mathf.Clamp(_currentXRotation - lookVec.y * sensitivity, -_verticalClamp, _verticalClamp);
            _playerCamera.transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
        }
    }

    public IEnumerator FocusViewOn(Vector3 worldTarget, float duration)
    {
        if (_playerCamera == null) yield break;

        Vector3 flatDirection = worldTarget - transform.position;
        flatDirection.y = 0f;

        Quaternion startBodyRotation = _rigidbody != null ? _rigidbody.rotation : transform.rotation;
        Quaternion targetBodyRotation = flatDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(flatDirection.normalized, Vector3.up)
            : startBodyRotation;

        Vector3 cameraToTarget = worldTarget - _playerCamera.transform.position;
        float horizontalDistance = new Vector2(cameraToTarget.x, cameraToTarget.z).magnitude;
        float targetPitch = -Mathf.Atan2(cameraToTarget.y, Mathf.Max(0.001f, horizontalDistance)) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, -_verticalClamp, _verticalClamp);
        float startPitch = _currentXRotation;

        if (_rigidbody != null)
        {
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (duration <= 0f)
        {
            ApplyViewRotation(targetBodyRotation, targetPitch);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            Quaternion bodyRotation = Quaternion.Slerp(startBodyRotation, targetBodyRotation, easedProgress);
            float cameraPitch = Mathf.Lerp(startPitch, targetPitch, easedProgress);
            ApplyViewRotation(bodyRotation, cameraPitch);
            yield return null;
        }

        ApplyViewRotation(targetBodyRotation, targetPitch);
    }

    private void ApplyViewRotation(Quaternion bodyRotation, float cameraPitch)
    {
        if (_rigidbody != null)
        {
            _rigidbody.rotation = bodyRotation;
        }
        else
        {
            transform.rotation = bodyRotation;
        }

        _currentXRotation = cameraPitch;
        _playerCamera.transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
    }

    private void CheckInteractableHover()
    {
        // Safety recovery: if UI returned to DEFAULT mode, ensure player state is back to Idle
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() == PlayerState.Interacting)
        {
            if (UIManager.Instance != null && UIManager.Instance.GetCurrentState() == UIState.DEFAULT)
            {
                _playerStateMachine.ChangeState(PlayerState.Idle);
            }
        }

        // Don't detect or highlight interactables when the player is not in Idle state
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() != PlayerState.Idle)
        {
            ClearHoveredInteractable();
            return;
        }

        int hitCount = RaycastSortedNonAlloc();
        IInteractable interactable = FindInteractableInHits(hitCount);

        if (interactable != null)
        {
            if (_currentHoveredStandaloneOutline != null)
            {
                _currentHoveredStandaloneOutline.SetOutlineActive(false);
                _currentHoveredStandaloneOutline = null;
            }

            if (_currentHoveredInteractable != interactable)
            {
                if (_currentHoveredInteractable != null)
                {
                    _currentHoveredInteractable.SetOutlineActive(false);
                }

                _currentHoveredInteractable = interactable;
                _isShowingHoverMessage = true;

                _currentHoveredInteractable.SetOutlineActive(true);

                if (UIManager.Instance != null)
                {
                    ShowHoverPrompt(interactable);
                }
            }
            else
            {
                _currentHoveredInteractable.UpdateHoveredOutlineColor();
                RefreshHoverPromptIfChanged(interactable);
            }
        }
        else
        {
            InteractableOutline standaloneOutline = FindStandaloneOutlineInHits(hitCount);

            if (standaloneOutline != null)
            {
                if (_currentHoveredInteractable != null)
                {
                    _currentHoveredInteractable.SetOutlineActive(false);
                    _currentHoveredInteractable = null;
                }

                if (_currentHoveredStandaloneOutline != standaloneOutline)
                {
                    if (_currentHoveredStandaloneOutline != null)
                    {
                        _currentHoveredStandaloneOutline.SetOutlineActive(false);
                    }

                    _currentHoveredStandaloneOutline = standaloneOutline;
                    _currentHoveredStandaloneOutline.SetOutlineActive(true);
                }

                _lastHoveredCanInteract = false;
                _lastHoveredMessage = null;

                if (_isShowingHoverMessage)
                {
                    _isShowingHoverMessage = false;
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.HideMessage();
                    }
                }
            }
            else
            {
                ClearHoveredInteractable();
            }
        }
    }

    private void ShowHoverPrompt(IInteractable interactable)
    {
        if (interactable == null || UIManager.Instance == null)
        {
            return;
        }

        bool canInteractNow = interactable.CanInteract();
        string messageToShow = canInteractNow
            ? interactable.InteractionMessage()
            : interactable.NotAvailableMessage();
        Color messageColor = canInteractNow
            ? Color.white
            : UIManager.UnavailableInteractionColor;

        _lastHoveredCanInteract = canInteractNow;
        _lastHoveredMessage = messageToShow;
        _isShowingHoverMessage = true;
        UIManager.Instance.ShowMessage(messageToShow, messageColor);
    }

    private void RefreshHoverPromptIfChanged(IInteractable interactable)
    {
        if (interactable == null || UIManager.Instance == null)
        {
            return;
        }

        bool canInteractNow = interactable.CanInteract();
        string currentMessage = canInteractNow
            ? interactable.InteractionMessage()
            : interactable.NotAvailableMessage();

        if (!_isShowingHoverMessage ||
            canInteractNow != _lastHoveredCanInteract ||
            !string.Equals(currentMessage, _lastHoveredMessage, System.StringComparison.Ordinal))
        {
            ShowHoverPrompt(interactable);
        }
    }

    public void TryInteract()
    {
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() != PlayerState.Idle)
        {
            return;
        }

        if ((DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) ||
            (UIManager.Instance != null && UIManager.Instance.GetCurrentState() == UIState.CONVERSATION))
        {
            return;
        }

        IInteractable interactable = GetInteractableInSight();
        Debug.Log("Interacted tried");

        if (interactable != null)
        {
            Debug.Log("Interacted");
            if (interactable.CanInteract())
            {
                _playerStateMachine.ChangeState(PlayerState.Interacting);
                interactable.Interact();
            }
            else
            {
                interactable.Interact();
            }
        }
        else
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowTemporaryMessage("Nothing to interact", new Color(0.3f, 0.3f, 0.3f), 2f);
            }
        }
    }

    private int RaycastSortedNonAlloc()
    {
        if (_playerCamera == null) return 0;

        Vector3 origin = _playerCamera.transform.position;
        Vector3 dir = _playerCamera.transform.forward;

        int hitCount = Physics.RaycastNonAlloc(origin, dir, _raycastHits, _interactDistance);
        for (int i = 1; i < hitCount; i++)
        {
            RaycastHit key = _raycastHits[i];
            int j = i - 1;
            while (j >= 0 && _raycastHits[j].distance > key.distance)
            {
                _raycastHits[j + 1] = _raycastHits[j];
                j--;
            }
            _raycastHits[j + 1] = key;
        }

        return hitCount;
    }

    private IInteractable GetInteractableInSight()
    {
        int hitCount = RaycastSortedNonAlloc();
        return FindInteractableInHits(hitCount);
    }

    private IInteractable FindInteractableInHits(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _raycastHits[i];
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject == gameObject || hitObject.transform.root == transform.root) continue;

            // Allow interactables that use a trigger volume as their targeting area,
            // while ignoring non-interactable trigger volumes so they don't block the ray.
            IInteractable interactable = hitObject.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                return interactable;
            }

            if (hit.collider.isTrigger) continue;

            // Hit an obstacle/wall that is not an interactable; stop ray
            break;
        }
        return null;
    }

    private InteractableOutline GetStandaloneOutlineInSight()
    {
        int hitCount = RaycastSortedNonAlloc();
        return FindStandaloneOutlineInHits(hitCount);
    }

    private InteractableOutline FindStandaloneOutlineInHits(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _raycastHits[i];
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject == gameObject || hitObject.transform.root == transform.root) continue;
            if (hit.collider.isTrigger) continue;

            InteractableOutline outline = hitObject.GetComponentInParent<InteractableOutline>();
            if (outline != null)
            {
                return outline;
            }

            break;
        }
        return null;
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        _mouseSensitivity = sensitivity;
    }
}
