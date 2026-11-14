using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scalable Animator and facing-orientation controller for single or multi-NPC encounters.
/// Listens to an <see cref="NPCData"/> interaction lifecycle, fires Animator triggers on all
/// participating characters, smoothly turns them toward the player, and restores their
/// initial transforms and idle animations when the conversation ends.
/// </summary>
[DisallowMultipleComponent]
public class NPCConversationReaction : MonoBehaviour
{
    [System.Serializable]
    public class ParticipantConfig
    {
        [Tooltip("Transform to rotate toward the player (defaults to the Animator transform if omitted).")]
        public Transform visualRoot;

        [Tooltip("Character Animator (auto-resolved from Visual Root if omitted).")]
        public Animator animator;

        [Tooltip("Yaw offset in degrees if the rig's visual forward is not +Z.")]
        public float forwardYawOffset;

        [Tooltip("Optional Animator trigger name override when interaction begins.")]
        public string interactTriggerOverride;

        [Tooltip("Optional Animator trigger name override when conversation ends.")]
        public string endTriggerOverride;
    }

    private sealed class ParticipantRuntime
    {
        public Transform VisualRoot;
        public Animator Animator;
        public float ForwardYawOffset;
        public int InteractTriggerHash;
        public int EndTriggerHash;
        public bool HasInteractTrigger;
        public bool HasEndTrigger;
        public Vector3 InitialLocalPosition;
        public Quaternion InitialLocalRotation;
    }

    [Header("Interaction Source")]
    [SerializeField] private NPCData _npcData;

    [Header("Participants")]
    [Tooltip("If empty, automatically collects all child Animators with a RuntimeAnimatorController.")]
    [SerializeField] private bool _autoDiscoverChildAnimators = true;
    [SerializeField] private List<ParticipantConfig> _participants = new List<ParticipantConfig>();

    [Header("Animator Triggers")]
    [SerializeField] private string _interactTriggerName = "PlayerInteracted";
    [SerializeField] private string _conversationEndedTriggerName = "ConversationEnded";

    [Header("Facing Behavior")]
    [SerializeField] private bool _rotateTowardPlayer = true;
    [SerializeField] private bool _returnToInitialPoseOnEnd = true;
    [SerializeField, Min(0.1f)] private float _rotationSpeed = 8f;

    private readonly List<ParticipantRuntime> _runtimes = new List<ParticipantRuntime>();
    private Coroutine _poseCoroutine;
    private bool _initialized;
    private bool _isConversationActive;

    private void Awake()
    {
        ResolveNpcData();
        InitializeParticipants();
    }

    private void OnEnable()
    {
        ResolveNpcData();
        InitializeParticipants();

        if (_npcData != null)
        {
            _npcData.InteractionStarted += HandleInteractionStarted;
            _npcData.InteractionEnded += HandleInteractionEnded;
        }
    }

    private void OnDisable()
    {
        if (_npcData != null)
        {
            _npcData.InteractionStarted -= HandleInteractionStarted;
            _npcData.InteractionEnded -= HandleInteractionEnded;
        }

        if (_poseCoroutine != null)
        {
            StopCoroutine(_poseCoroutine);
            _poseCoroutine = null;
        }

        _isConversationActive = false;
    }

    private void ResolveNpcData()
    {
        if (_npcData == null)
        {
            _npcData = GetComponent<NPCData>();
        }
    }

    private void InitializeParticipants()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _runtimes.Clear();

        if (_participants != null && _participants.Count > 0)
        {
            foreach (ParticipantConfig config in _participants)
            {
                TryRegisterParticipant(config);
            }
            return;
        }

        if (!_autoDiscoverChildAnimators)
        {
            return;
        }

        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
        foreach (Animator childAnimator in childAnimators)
        {
            if (childAnimator == null || childAnimator.runtimeAnimatorController == null)
            {
                continue;
            }

            RegisterRuntime(
                childAnimator.transform,
                childAnimator,
                0f,
                _interactTriggerName,
                _conversationEndedTriggerName);
        }
    }

    private void TryRegisterParticipant(ParticipantConfig config)
    {
        if (config == null)
        {
            return;
        }

        Animator anim = config.animator;
        Transform root = config.visualRoot;

        if (anim == null && root != null)
        {
            anim = root.GetComponentInChildren<Animator>(true);
        }

        if (root == null && anim != null)
        {
            root = anim.transform;
        }

        if (root == null && anim == null)
        {
            return;
        }

        string interactName = string.IsNullOrWhiteSpace(config.interactTriggerOverride)
            ? _interactTriggerName
            : config.interactTriggerOverride;
        string endName = string.IsNullOrWhiteSpace(config.endTriggerOverride)
            ? _conversationEndedTriggerName
            : config.endTriggerOverride;

        RegisterRuntime(root, anim, config.forwardYawOffset, interactName, endName);
    }

    private void RegisterRuntime(
        Transform root,
        Animator animator,
        float forwardYawOffset,
        string interactTriggerName,
        string endTriggerName)
    {
        int interactHash = string.IsNullOrEmpty(interactTriggerName) ? 0 : Animator.StringToHash(interactTriggerName);
        int endHash = string.IsNullOrEmpty(endTriggerName) ? 0 : Animator.StringToHash(endTriggerName);

        _runtimes.Add(new ParticipantRuntime
        {
            VisualRoot = root,
            Animator = animator,
            ForwardYawOffset = forwardYawOffset,
            InteractTriggerHash = interactHash,
            EndTriggerHash = endHash,
            HasInteractTrigger = HasTriggerParameter(animator, interactHash),
            HasEndTrigger = HasTriggerParameter(animator, endHash),
            InitialLocalPosition = root != null ? root.localPosition : Vector3.zero,
            InitialLocalRotation = root != null ? root.localRotation : Quaternion.identity
        });
    }

    private static bool HasTriggerParameter(Animator animator, int nameHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null || nameHash == 0)
        {
            return false;
        }

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Trigger && param.nameHash == nameHash)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleInteractionStarted()
    {
        InitializeParticipants();
        _isConversationActive = true;

        foreach (ParticipantRuntime participant in _runtimes)
        {
            FireTrigger(participant, toActiveState: true);
        }

        if (_rotateTowardPlayer)
        {
            RestartPoseCoroutine(RotateTowardPlayerRoutine());
        }
    }

    private void HandleInteractionEnded()
    {
        if (!_isConversationActive)
        {
            return;
        }

        _isConversationActive = false;

        foreach (ParticipantRuntime participant in _runtimes)
        {
            FireTrigger(participant, toActiveState: false);
        }

        if (_returnToInitialPoseOnEnd)
        {
            RestartPoseCoroutine(RestoreInitialPoseRoutine());
        }
    }

    private static void FireTrigger(ParticipantRuntime participant, bool toActiveState)
    {
        Animator anim = participant.Animator;
        if (anim == null || !anim.isActiveAndEnabled)
        {
            return;
        }

        if (toActiveState)
        {
            if (participant.HasEndTrigger) anim.ResetTrigger(participant.EndTriggerHash);
            if (participant.HasInteractTrigger) anim.SetTrigger(participant.InteractTriggerHash);
        }
        else
        {
            if (participant.HasInteractTrigger) anim.ResetTrigger(participant.InteractTriggerHash);
            if (participant.HasEndTrigger) anim.SetTrigger(participant.EndTriggerHash);
        }
    }

    private void RestartPoseCoroutine(IEnumerator routine)
    {
        if (_poseCoroutine != null)
        {
            StopCoroutine(_poseCoroutine);
        }

        _poseCoroutine = StartCoroutine(routine);
    }

    private IEnumerator RotateTowardPlayerRoutine()
    {
        const float AngleEpsilon = 0.25f;

        while (_isConversationActive)
        {
            Transform playerTransform = ResolvePlayerTransform();
            if (playerTransform == null)
            {
                yield break;
            }

            bool allAligned = true;
            float t = 1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime);

            foreach (ParticipantRuntime participant in _runtimes)
            {
                Transform root = participant.VisualRoot;
                if (root == null)
                {
                    continue;
                }

                Quaternion targetWorldRot = ComputeHorizontalLookRotation(
                    root,
                    playerTransform.position,
                    participant.ForwardYawOffset);

                if (Quaternion.Angle(root.rotation, targetWorldRot) > AngleEpsilon)
                {
                    root.rotation = Quaternion.Slerp(root.rotation, targetWorldRot, t);
                    allAligned = false;
                }
                else
                {
                    root.rotation = targetWorldRot;
                }
            }

            if (allAligned)
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator RestoreInitialPoseRoutine()
    {
        const float AngleEpsilon = 0.25f;
        const float PositionEpsilonSqr = 0.000001f;

        while (true)
        {
            bool allRestored = true;
            float t = 1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime);

            foreach (ParticipantRuntime participant in _runtimes)
            {
                Transform root = participant.VisualRoot;
                if (root == null)
                {
                    continue;
                }

                if (Quaternion.Angle(root.localRotation, participant.InitialLocalRotation) > AngleEpsilon)
                {
                    root.localRotation = Quaternion.Slerp(root.localRotation, participant.InitialLocalRotation, t);
                    allRestored = false;
                }
                else
                {
                    root.localRotation = participant.InitialLocalRotation;
                }

                if ((root.localPosition - participant.InitialLocalPosition).sqrMagnitude > PositionEpsilonSqr)
                {
                    root.localPosition = Vector3.Lerp(root.localPosition, participant.InitialLocalPosition, t);
                    allRestored = false;
                }
                else
                {
                    root.localPosition = participant.InitialLocalPosition;
                }
            }

            if (allRestored)
            {
                _poseCoroutine = null;
                yield break;
            }

            yield return null;
        }
    }

    private static Transform ResolvePlayerTransform()
    {
        if (PlayerController.Instance != null)
        {
            return PlayerController.Instance.transform;
        }

        Camera mainCam = Camera.main;
        return mainCam != null ? mainCam.transform : null;
    }

    private static Quaternion ComputeHorizontalLookRotation(
        Transform target,
        Vector3 worldTargetPos,
        float forwardYawOffset)
    {
        Vector3 toTarget = worldTargetPos - target.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return target.rotation;
        }

        Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        return Mathf.Approximately(forwardYawOffset, 0f)
            ? look
            : look * Quaternion.Euler(0f, forwardYawOffset, 0f);
    }
}
