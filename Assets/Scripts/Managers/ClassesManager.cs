using System;
using System.Collections.Generic;
using UnityEngine;

public class ClassesManager : MonoBehaviour
{
    public static ClassesManager Instance { get; private set; }

    [SerializeField]
    private float _ringTimer = 30f;

    [SerializeField]
    private string _ringAudio = null;

    private float _currentTime = 0f;

    private bool _timerActive = false;

    [SerializeField]
    private List<string> _activateEvents = new List<string>();

    [SerializeField]
    private List<string> _deactivateEvents = new List<string>();

    [Header("Classes Attended")]
    [Tooltip("Message displayed on the transition when classes are attended.")]
    [SerializeField]
    private string _classesAttendedMessage = "6 hours later...";

    [Tooltip("Events that trigger the classes attended transition.")]
    [SerializeField]
    private List<string> _classesAttendedEvents = new List<string>() { "Attend_Classes" };

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

    void Start()
    {
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnsubscribeEvents();
        }
    }

    void Update()
    {
        if (_timerActive)
        {
            _currentTime += Time.deltaTime;
            if (_currentTime >= _ringTimer)
            {
                EventBus.Instance.Publish("Class_Started");
                ResetTimer();
            }
        }   
    }

    /// <summary>
    /// Method to reset all the variables related to the timer
    /// </summary>
    private void ResetTimer()
    {
        Activate(false);
        _currentTime = 0f;
    }

    /// <summary>
    /// Method to activate or deactivate the class timer
    /// </summary>
    /// <param name="activate"></param>
    public void Activate(bool activate)
    {
        _timerActive = activate;
    }

    public void ChangeRingTimer(float newTimer)
    {
        _ringTimer = newTimer;
    } 

    private void SubscribeEvents()
    {
        if (EventBus.Instance == null) return;

        if (_activateEvents != null)
        {
            foreach (var ev in _activateEvents)
            {
                if (!string.IsNullOrEmpty(ev))
                {
                    EventBus.Instance.Subscribe(ev, OnActivateEvent);
                }
            }
        }

        if (_deactivateEvents != null)
        {
            foreach (var ev in _deactivateEvents)
            {
                if (!string.IsNullOrEmpty(ev))
                {
                    EventBus.Instance.Subscribe(ev, OnDeactivateEvent);
                }
            }
        }

        if (_classesAttendedEvents != null)
        {
            foreach (var ev in _classesAttendedEvents)
            {
                if (!string.IsNullOrEmpty(ev))
                {
                    EventBus.Instance.Subscribe(ev, OnClassesAttended);
                }
            }
        }
    }

    private void UnsubscribeEvents()
    {
        if (EventBus.Instance == null) return;

        if (_activateEvents != null)
        {
            foreach (var ev in _activateEvents)
            {
                if (!string.IsNullOrEmpty(ev))
                {
                    EventBus.Instance.Unsubscribe(ev, OnActivateEvent);
                }
            }
        }

        if (_deactivateEvents != null)
        {
            foreach (var ev in _deactivateEvents)
            {
                if (!string.IsNullOrEmpty(ev))
                {
                    EventBus.Instance.Unsubscribe(ev, OnDeactivateEvent);
                }
            }
        }

        if (_classesAttendedEvents != null)
        {
            foreach (var ev in _classesAttendedEvents)
            {
                if (!string.IsNullOrEmpty(ev))
                {
                    EventBus.Instance.Unsubscribe(ev, OnClassesAttended);
                }
            }
        }
    }

    private void OnActivateEvent()
    {
        Activate(true);
    }

    private void OnDeactivateEvent()
    {
        Activate(false);
    }

    public void OnClassesAttended()
    {
        ResetTimer();

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.PlayTextTransition(_classesAttendedMessage);
        }
    }
}
