using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class BookshelfInteractable : IInteractable
{
    [Header("Bookshelf Preview")]
    [Tooltip("How long the temporary Bookshelf message remains on the black screen.")]
    [SerializeField, Min(0f)] private float _bookshelfScreenDuration = 2.0f;

    protected override void Awake()
    {
        if (string.IsNullOrWhiteSpace(_interactMessage))
        {
            _interactMessage = "Use bookshelf";
        }

        if (string.IsNullOrWhiteSpace(_notAvailableMessage))
        {
            _notAvailableMessage = "Bookshelf is unavailable";
        }

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (boxCollider != null && meshFilter != null && meshFilter.sharedMesh != null)
        {
            if (boxCollider.size == Vector3.one && boxCollider.center == Vector3.zero)
            {
                Bounds bounds = meshFilter.sharedMesh.bounds;
                if (bounds.size.sqrMagnitude > 0.000001f)
                {
                    boxCollider.center = bounds.center;
                    boxCollider.size = bounds.size;
                }
            }
        }

        base.Awake();
    }

    public override void Interact()
    {
        if (!_canInteract || TransitionManager.Instance == null)
        {
            ReturnPlayerToIdle();
            return;
        }

        TransitionManager.Instance.PlayMessageCard("<b>Bookshelf</b>", _bookshelfScreenDuration);
    }

    private static void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }
}
