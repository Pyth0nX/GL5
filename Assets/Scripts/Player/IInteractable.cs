using System;
using UnityEngine;

public abstract class IInteractable : MonoBehaviour
{
    [Tooltip("Can this object be interacted with right now?")]
    [SerializeField] protected bool _canInteract = true;

    [Header("Messages to show when you try to interact with this object")]
    [SerializeField] protected string _interactMessage;
    [SerializeField] protected string _notAvailableMessage;

    [Header("Outline Colors")]
    [Tooltip("Color of the outline when the object can be interacted with.")]
    [SerializeField] protected Color _availableColor = new Color(0.2f, 0.85f, 0.2f, 1f);

    [Tooltip("Color of the outline when the object cannot be interacted with right now.")]
    [SerializeField] protected Color _notAvailableColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    [Header("Outline Highlight (New)")]
    [Tooltip("InteractableOutline component(s) on this interactable or its children/parents to toggle on hover.")]
    [SerializeField] protected InteractableOutline[] _interactableOutlines;

    [Tooltip("If true, automatically creates an InteractableOutline component if none is found.")]
    [SerializeField] protected bool _autoAddOutline = true;

    [Tooltip("Default thickness applied when an InteractableOutline is automatically created.")]
    [Range(0.1f, 10f)]
    [SerializeField] protected float _defaultOutlineWidth = 2.5f;

    [Header("Legacy Outline (Deprecated)")]
    [Tooltip("Legacy Outline component(s) maintained for backwards compatibility.")]
    [SerializeField, HideInInspector] protected Outline[] _outlines;

    abstract public void Interact();

    public string InteractionMessage() { return _interactMessage; }
    public string NotAvailableMessage() { return _notAvailableMessage; }
    public bool CanInteract() { return _canInteract; }
    public Color AvailableColor { get => _availableColor; set => _availableColor = value; }
    public Color NotAvailableColor { get => _notAvailableColor; set => _notAvailableColor = value; }

    protected virtual void Awake()
    {
        DisableLegacyOutlines();
        CacheOutlines();
        SetOutlineActive(false);
    }

    protected virtual void Start()
    {
        SetOutlineActive(false);
    }

    private void DisableLegacyOutlines()
    {
        // Disable legacy QuickOutline components to prevent shader/material conflicts
        Outline[] legacy = GetComponentsInChildren<Outline>(true);
        for (int i = 0; i < legacy.Length; i++)
        {
            if (legacy[i] != null)
            {
                legacy[i].enabled = false;
            }
        }
    }

    public void CacheOutlines()
    {
        // 1. Search for InteractableOutline components
        if (_interactableOutlines == null || _interactableOutlines.Length == 0)
        {
            _interactableOutlines = GetComponentsInChildren<InteractableOutline>(true);
            if (_interactableOutlines == null || _interactableOutlines.Length == 0)
            {
                _interactableOutlines = GetComponentsInParent<InteractableOutline>(true);
            }
        }

        // 2. If still none found and auto-add is enabled, automatically add one to this GameObject
        if ((_interactableOutlines == null || _interactableOutlines.Length == 0) && _autoAddOutline)
        {
            InteractableOutline autoOutline = GetComponent<InteractableOutline>();
            if (autoOutline == null)
            {
                autoOutline = gameObject.AddComponent<InteractableOutline>();
                autoOutline.OutlineColor = CanInteract() ? _availableColor : _notAvailableColor;
                autoOutline.OutlineWidth = _defaultOutlineWidth;
            }
            _interactableOutlines = new InteractableOutline[] { autoOutline };
        }

        // 3. Fallback cache for legacy outlines if present
        if (_outlines == null || _outlines.Length == 0)
        {
            _outlines = GetComponentsInChildren<Outline>(true);
            if (_outlines == null || _outlines.Length == 0)
            {
                _outlines = GetComponentsInParent<Outline>(true);
            }
        }
    }

    /// <summary>
    /// Enables or disables the outline component(s) for this interactable, setting the color based on availability.
    /// </summary>
    public virtual void SetOutlineActive(bool active)
    {
        CacheOutlines();

        Color targetColor = CanInteract() ? _availableColor : _notAvailableColor;

        if (_interactableOutlines != null)
        {
            for (int i = 0; i < _interactableOutlines.Length; i++)
            {
                if (_interactableOutlines[i] != null)
                {
                    if (active)
                    {
                        _interactableOutlines[i].OutlineColor = targetColor;
                    }
                    _interactableOutlines[i].SetOutlineActive(active);
                }
            }
        }

        // Legacy outline fallback if any is still in use
        if (_outlines != null)
        {
            for (int i = 0; i < _outlines.Length; i++)
            {
                if (_outlines[i] != null && _outlines[i].enabled != active)
                {
                    _outlines[i].OutlineColor = targetColor;
                    _outlines[i].enabled = active;
                }
            }
        }
    }

    /// <summary>
    /// Updates the outline color while the object is currently being hovered/looked at.
    /// </summary>
    public virtual void UpdateHoveredOutlineColor()
    {
        if (_interactableOutlines == null) return;

        Color targetColor = CanInteract() ? _availableColor : _notAvailableColor;
        for (int i = 0; i < _interactableOutlines.Length; i++)
        {
            if (_interactableOutlines[i] != null && _interactableOutlines[i].IsOutlineActive)
            {
                _interactableOutlines[i].OutlineColor = targetColor;
            }
        }
    }
}