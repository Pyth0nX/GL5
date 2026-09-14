using UnityEngine;
using System;
using System.Collections.Generic;
public enum EventType
{
    ChangeScene,
    ChangeDialogues,
    ChangeData
}


public class EventBus : MonoBehaviour
{
    public static EventBus Instance { get; private set; }

    private Dictionary<EventType, List<Delegate>> eventHandlers;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void Suscribe<T>(Action<T> listener)
    {

    }

    public void Unsubscribe<T>(Action<T> listener)
    {

    }



}
