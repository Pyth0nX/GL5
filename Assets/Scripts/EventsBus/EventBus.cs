using UnityEngine;
using System;
using System.Collections.Generic;

public class EventBus : MonoBehaviour
{
    public static EventBus Instance { get; private set; }

    private Dictionary<string, Action> _events = new Dictionary<string, Action>();
    private Dictionary<string, Delegate> _typedEvents = new Dictionary<string, Delegate>();

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


    public void Subscribe<T>(string eventName, Action<T> listener)
    {
        if (!_typedEvents.ContainsKey(eventName)) _typedEvents[eventName] = null;
        _typedEvents[eventName] = (Action<T>)_typedEvents[eventName] + listener;
    }

    public void Unsubscribe<T>(string eventName, Action<T> listener)
    {
        if (_typedEvents.ContainsKey(eventName)) _typedEvents[eventName] = (Action<T>)_typedEvents[eventName] - listener;
    }

    public void Publish<T>(string eventName, T payload)
    {
        if (_typedEvents.TryGetValue(eventName, out var del)) 
        {
            (del as Action<T>)?.Invoke(payload);
        } 
    }
}
