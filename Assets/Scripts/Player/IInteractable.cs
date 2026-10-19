using System;
using UnityEngine;

public abstract class IInteractable : MonoBehaviour
{
    [Tooltip("Can this object be interacted with right now?")]
    [SerializeField] protected bool _canInteract = true;

    [Header("Messages to show when you try to interact with this object")]
    [SerializeField] protected string _interactMessage;
    [SerializeField] protected string _notAvailableMessage;
    abstract public void Interact();

    public string InteractionMessage() { return _interactMessage; }
    public string NotAvailableMessage() { return _notAvailableMessage; }
    public bool CanInteract() { return _canInteract; }

}
