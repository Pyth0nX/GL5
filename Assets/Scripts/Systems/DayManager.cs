using System.Collections;
using UnityEngine;

public class DayManager : MonoBehaviour
{
    public static DayManager Instance { get; private set; }

    [SerializeField] private int _currentDay = 0;

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

    private void Start()
    {
        EventBus.Instance.Subscribe("NextDay", OnNextDay);
    }

    private IEnumerator SendStartDayEventDelayed()
    {
        yield return new WaitForSeconds(1f);
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish($"Start_Day{_currentDay}");
        }
    }


    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("NextDay", OnNextDay);
        }
    }

    public void ResetDay()
    {
        _currentDay = 0;
    }

    /// <summary>
    /// Directly sets the current day when restoring a saved game without re-triggering day start events.
    /// </summary>
    public void SetCurrentDay(int day)
    {
        _currentDay = Mathf.Max(0, day);
    }

    private void OnNextDay()
    {
        _currentDay++;
        Debug.Log($"[DayManager] Advanced to Day {_currentDay}");
        
        // Broadcast the specific day start event so EventNodeUpdaters can catch it
        // e.g., "Start_Day2", "Start_Day3"
        EventBus.Instance.Publish($"Start_Day{_currentDay}");
    }

    public int GetCurrentDay()
    {
        return _currentDay;
    }
}
