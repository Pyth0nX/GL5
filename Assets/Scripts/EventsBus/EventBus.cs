using UnityEngine;
using System;
using System.Collections.Generic;

public class EventBus : MonoBehaviour
{
    public static EventBus Instance { get; private set; }

    private Dictionary<string, Action> _events = new Dictionary<string, Action>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Subscribe to an event using a string identifier.
    /// </summary>
    public void Subscribe(string eventName, Action listener)
    {
        if (!_events.ContainsKey(eventName))
        {
            _events[eventName] = null;
        }
        _events[eventName] += listener;
    }

    /// <summary>
    /// Unsubscribe from an event.
    /// </summary>
    public void Unsubscribe(string eventName, Action listener)
    {
        if (_events.ContainsKey(eventName))
        {
            _events[eventName] -= listener;
        }
    }

    /// <summary>
    /// Publish an event to all subscribers.
    /// </summary>
    public void Publish(string eventName)
    {
        if (_events.ContainsKey(eventName))
        {
            _events[eventName]?.Invoke();
        }
    }

    // --- String Payload Events ---
    private Dictionary<string, Action<string>> _stringEvents = new Dictionary<string, Action<string>>();

    public void Subscribe(string eventName, Action<string> listener)
    {
        if (!_stringEvents.ContainsKey(eventName)) _stringEvents[eventName] = null;
        _stringEvents[eventName] += listener;
    }

    public void Unsubscribe(string eventName, Action<string> listener)
    {
        if (_stringEvents.ContainsKey(eventName)) _stringEvents[eventName] -= listener;
    }

    public void Publish(string eventName, string payload)
    {
        if (_stringEvents.ContainsKey(eventName)) _stringEvents[eventName]?.Invoke(payload);
    }
}
