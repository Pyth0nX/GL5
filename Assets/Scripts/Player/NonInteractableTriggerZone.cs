using System;
using UnityEngine;

/// <summary>
/// Marks a spatial trigger collider so interaction raycasts always pass through it
/// without treating it or its parent hierarchy as an IInteractable hover/click target.
/// Optionally forwards OnTriggerEnter events to a parent controller.
/// </summary>
[DisallowMultipleComponent]
public sealed class NonInteractableTriggerZone : MonoBehaviour
{
    public event Action<Collider> TriggerEntered;

    private void OnTriggerEnter(Collider other)
    {
        TriggerEntered?.Invoke(other);
    }
}
