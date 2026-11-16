using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Updates an NPC's local position, rotation, and active state based on the current story day
/// so classmates move around the classroom/school from day to day.
/// </summary>
[DisallowMultipleComponent]
public class NPCDailySchedule : MonoBehaviour
{
    [Serializable]
    public struct DayPose
    {
        public int day;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public bool isActive;
    }

    [SerializeField] private List<DayPose> _dayPoses = new List<DayPose>();

    private bool _capturedDefault;
    private Vector3 _defaultLocalPos;
    private Quaternion _defaultLocalRot;

    private void Awake()
    {
        CaptureDefaultPose();
        EnsureDefaultScheduleIfEmpty();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        ApplyCurrentDayPose();
    }

    private void Start()
    {
        ApplyCurrentDayPose();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void ApplyCurrentDayPose()
    {
        CaptureDefaultPose();
        EnsureDefaultScheduleIfEmpty();

        int day = DayManager.Instance != null ? Mathf.Max(1, DayManager.Instance.GetCurrentDay()) : 1;
        if (_dayPoses.Count == 0)
        {
            return;
        }

        DayPose? match = null;
        for (int i = 0; i < _dayPoses.Count; i++)
        {
            if (_dayPoses[i].day == day)
            {
                match = _dayPoses[i];
                break;
            }
        }

        if (!match.HasValue)
        {
            match = _dayPoses[(day - 1) % _dayPoses.Count];
        }

        DayPose pose = match.Value;
        transform.localPosition = pose.localPosition;
        transform.localRotation = Quaternion.Euler(pose.localEulerAngles);

        if (gameObject.activeSelf != pose.isActive)
        {
            gameObject.SetActive(pose.isActive);
        }
    }

    private void CaptureDefaultPose()
    {
        if (_capturedDefault)
        {
            return;
        }

        _capturedDefault = true;
        _defaultLocalPos = transform.localPosition;
        _defaultLocalRot = transform.localRotation;
    }

    private void SubscribeEvents()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Subscribe("NextDay", ApplyCurrentDayPose);
        EventBus.Instance.Subscribe("Start_Day1", ApplyCurrentDayPose);
        EventBus.Instance.Subscribe("Start_Day2", ApplyCurrentDayPose);
        EventBus.Instance.Subscribe("Start_Day3", ApplyCurrentDayPose);
        EventBus.Instance.Subscribe("Start_Day4", ApplyCurrentDayPose);
        EventBus.Instance.Subscribe("Start_Day5", ApplyCurrentDayPose);
    }

    private void UnsubscribeEvents()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Unsubscribe("NextDay", ApplyCurrentDayPose);
        EventBus.Instance.Unsubscribe("Start_Day1", ApplyCurrentDayPose);
        EventBus.Instance.Unsubscribe("Start_Day2", ApplyCurrentDayPose);
        EventBus.Instance.Unsubscribe("Start_Day3", ApplyCurrentDayPose);
        EventBus.Instance.Unsubscribe("Start_Day4", ApplyCurrentDayPose);
        EventBus.Instance.Unsubscribe("Start_Day5", ApplyCurrentDayPose);
    }

    private void EnsureDefaultScheduleIfEmpty()
    {
        if (_dayPoses != null && _dayPoses.Count > 0)
        {
            return;
        }

        _dayPoses = BuildDefaultClassroomSchedule(gameObject.name.Trim(), _defaultLocalPos, _defaultLocalRot.eulerAngles);
    }

    private static List<DayPose> BuildDefaultClassroomSchedule(string objName, Vector3 day1Pos, Vector3 day1Euler)
    {
        // Verified classroom spots inside NPC parent bounds (x in [-6.11, 7.83], z in [-34.39, -23.40])
        Vector3 frontRight = new Vector3(7.83f, 0f, -34.39f);
        Vector3 frontCenter = new Vector3(0.83f, 0f, -34.39f);
        Vector3 frontLeft = new Vector3(-6.11f, 0f, -34.39f);
        Vector3 backRight = new Vector3(7.83f, 0f, -23.40f);
        Vector3 backCenter = new Vector3(0.83f, 0f, -23.40f);
        Vector3 backLeft = new Vector3(-6.11f, 0f, -23.40f);

        Vector3 faceFront = new Vector3(0f, 90f, 0f);
        Vector3 faceLeft = new Vector3(0f, 180f, 0f);
        Vector3 faceRight = new Vector3(0f, 0f, 0f);
        Vector3 faceCenter = new Vector3(0f, 45f, 0f);

        return objName switch
        {
            "NPC (5)" or "NPC(5)" => new List<DayPose> // Leo
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = backCenter, localEulerAngles = faceCenter, isActive = true },
                new DayPose { day = 3, localPosition = frontLeft, localEulerAngles = faceRight, isActive = true },
                new DayPose { day = 4, localPosition = backRight, localEulerAngles = faceLeft, isActive = true },
                new DayPose { day = 5, localPosition = frontCenter, localEulerAngles = faceFront, isActive = true }
            },
            "NPC (6)" or "NPC(6)" => new List<DayPose> // Chloe
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = new Vector3(5.6f, 0f, -28.8f), localEulerAngles = faceLeft, isActive = true },
                new DayPose { day = 3, localPosition = backLeft, localEulerAngles = faceRight, isActive = true },
                new DayPose { day = 4, localPosition = frontRight, localEulerAngles = faceFront, isActive = true },
                new DayPose { day = 5, localPosition = backCenter, localEulerAngles = faceCenter, isActive = true }
            },
            "NPC (8)" or "NPC(8)" => new List<DayPose> // Liam
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = frontRight, localEulerAngles = faceFront, isActive = true },
                new DayPose { day = 3, localPosition = new Vector3(-2.5f, 0f, -28.8f), localEulerAngles = faceRight, isActive = true },
                new DayPose { day = 4, localPosition = frontLeft, localEulerAngles = faceFront, isActive = true },
                new DayPose { day = 5, localPosition = backRight, localEulerAngles = faceLeft, isActive = true }
            },
            "NPC (9)" or "NPC(9)" => new List<DayPose> // Mia
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = new Vector3(3.1f, 0f, -28.8f), localEulerAngles = faceRight, isActive = true },
                new DayPose { day = 3, localPosition = frontRight, localEulerAngles = faceFront, isActive = true },
                new DayPose { day = 4, localPosition = backCenter, localEulerAngles = faceCenter, isActive = true },
                new DayPose { day = 5, localPosition = frontLeft, localEulerAngles = faceRight, isActive = true }
            },
            "NPC (10)" or "NPC(10)" => new List<DayPose> // Ethan
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = frontLeft, localEulerAngles = faceRight, isActive = true },
                new DayPose { day = 3, localPosition = backRight, localEulerAngles = faceLeft, isActive = true },
                new DayPose { day = 4, localPosition = new Vector3(-3.8f, 0f, -28.8f), localEulerAngles = faceRight, isActive = true },
                new DayPose { day = 5, localPosition = backLeft, localEulerAngles = faceFront, isActive = true }
            },
            _ => new List<DayPose>()
        };
    }
}
