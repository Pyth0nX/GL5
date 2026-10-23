using System;
using UnityEngine;

public abstract class IInteractable : MonoBehaviour
{
    [Tooltip("Can this object be interacted with right now?")]
    [SerializeField] protected bool _canInteract = true;

    [Header("Messages to show when you try to interact with this object")]
    [SerializeField] protected string _interactMessage;
    [SerializeField] protected string _notAvailableMessage;

    [Header("Outline Highlight (Optional)")]
    [Tooltip("Outline component(s) on this interactable or its children/parents to toggle on hover.")]
    [SerializeField] protected Outline[] _outlines;

    abstract public void Interact();

    public string InteractionMessage() { return _interactMessage; }
    public string NotAvailableMessage() { return _notAvailableMessage; }
    public bool CanInteract() { return _canInteract; }

    protected virtual void Awake()
    {
        CacheOutlines();
        SetOutlineActive(false);
    }

    protected virtual void Start()
    {
        SetOutlineActive(false);
    }

    public void CacheOutlines()
    {
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
    /// Enables or disables the outline component(s) for this interactable.
    /// </summary>
    public virtual void SetOutlineActive(bool active)
    {
        CacheOutlines();
        if (_outlines != null)
        {
            for (int i = 0; i < _outlines.Length; i++)
            {
                if (_outlines[i] != null && _outlines[i].enabled != active)
                {
                    _outlines[i].enabled = active;
                }
            }
        }
    }
}
