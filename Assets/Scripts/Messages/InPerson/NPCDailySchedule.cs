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

    // Front-center desk in the classroom belongs to the protagonist (LunaDesk / ClassDesk)
    private static readonly Vector3 ProtagonistDeskLocalPos = new Vector3(0.83f, 0f, -34.39f);
    private const float ProtagonistLocalClearanceXZ = 1.8f;
    private const float ProtagonistWorldClearanceXZ = 2.2f;
    private const float SiblingNpcClearanceXZ = 1.4f;

    // Verified classroom desk spots inside NPC parent bounds (x in [-6.11, 7.83], z in [-34.39, -23.40]).
    // ProtagonistDeskLocalPos (0.83, 0, -34.39) is intentionally excluded.
    private static readonly Vector3[] SafeClassroomSpots =
    {
        new Vector3(7.83f, 0f, -34.39f),  // Front right
        new Vector3(-6.11f, 0f, -34.39f), // Front left
        new Vector3(7.83f, 0f, -23.40f),  // Back right
        new Vector3(0.83f, 0f, -23.40f),  // Back center
        new Vector3(-6.11f, 0f, -23.40f), // Back left
    };

    private static readonly string[] DayChangeEvents =
    {
        "NextDay",
        "Start_Day1",
        "Start_Day2",
        "Start_Day3",
        "Start_Day4",
        "Start_Day5"
    };

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
        UpdateDayEventSubscriptions(subscribe: true);
        ApplyCurrentDayPose();
    }

    private void Start()
    {
        ApplyCurrentDayPose();
    }

    private void OnDisable()
    {
        UpdateDayEventSubscriptions(subscribe: false);
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
        transform.localPosition = ResolveSafePosition(pose.localPosition, day);
        transform.localRotation = Quaternion.Euler(pose.localEulerAngles);

        if (gameObject.activeSelf != pose.isActive)
        {
            gameObject.SetActive(pose.isActive);
        }
    }

    private Vector3 ResolveSafePosition(Vector3 desiredLocalPos, int day)
    {
        ClassDesk protagonistDesk = FindAnyObjectByType<ClassDesk>();
        if (!IsTooCloseToProtagonistDesk(desiredLocalPos, protagonistDesk))
        {
            return desiredLocalPos;
        }

        NPCDailySchedule[] siblings = FindObjectsByType<NPCDailySchedule>();

        // Prefer original scene spawn first if it clears both the protagonist's desk and sibling NPCs.
        if (IsSpotSafeAndUnoccupied(_defaultLocalPos, protagonistDesk, siblings))
        {
            Debug.LogWarning($"[NPCDailySchedule] '{gameObject.name}' Day {day} pose {desiredLocalPos} overlapped the protagonist's desk; redirected to default spot {_defaultLocalPos}.");
            return _defaultLocalPos;
        }

        // Otherwise pick the first verified unoccupied classroom spot so two NPCs never stack on one desk.
        for (int i = 0; i < SafeClassroomSpots.Length; i++)
        {
            Vector3 candidate = SafeClassroomSpots[i];
            if (IsSpotSafeAndUnoccupied(candidate, protagonistDesk, siblings))
            {
                Debug.LogWarning($"[NPCDailySchedule] '{gameObject.name}' Day {day} pose {desiredLocalPos} overlapped the protagonist's desk; redirected to {candidate}.");
                return candidate;
            }
        }

        Vector3 fallback = !IsTooCloseToProtagonistDesk(_defaultLocalPos, protagonistDesk)
            ? _defaultLocalPos
            : SafeClassroomSpots[0];

        Debug.LogWarning($"[NPCDailySchedule] '{gameObject.name}' Day {day} pose {desiredLocalPos} overlapped the protagonist's desk; redirected to {fallback}.");
        return fallback;
    }

    private bool IsSpotSafeAndUnoccupied(Vector3 candidateLocalPos, ClassDesk protagonistDesk, NPCDailySchedule[] siblings)
    {
        if (IsTooCloseToProtagonistDesk(candidateLocalPos, protagonistDesk))
        {
            return false;
        }

        if (siblings == null)
        {
            return true;
        }

        float minSq = SiblingNpcClearanceXZ * SiblingNpcClearanceXZ;
        for (int i = 0; i < siblings.Length; i++)
        {
            NPCDailySchedule sibling = siblings[i];
            if (sibling == null || sibling == this || !sibling.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (sibling.transform.parent != transform.parent)
            {
                continue;
            }

            Vector3 sibPos = sibling.transform.localPosition;
            float dx = candidateLocalPos.x - sibPos.x;
            float dz = candidateLocalPos.z - sibPos.z;
            if ((dx * dx + dz * dz) < minSq)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsTooCloseToProtagonistDesk(Vector3 localPos, ClassDesk protagonistDesk)
    {
        if (IsWithinXZDistance(localPos, ProtagonistDeskLocalPos, ProtagonistLocalClearanceXZ))
        {
            return true;
        }

        if (protagonistDesk != null)
        {
            Vector3 worldPos = transform.parent != null
                ? transform.parent.TransformPoint(localPos)
                : localPos;
            if (IsWithinXZDistance(worldPos, protagonistDesk.transform.position, ProtagonistWorldClearanceXZ))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithinXZDistance(Vector3 a, Vector3 b, float clearance)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return (dx * dx + dz * dz) < (clearance * clearance);
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

    private void UpdateDayEventSubscriptions(bool subscribe)
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        for (int i = 0; i < DayChangeEvents.Length; i++)
        {
            if (subscribe)
            {
                EventBus.Instance.Subscribe(DayChangeEvents[i], ApplyCurrentDayPose);
            }
            else
            {
                EventBus.Instance.Unsubscribe(DayChangeEvents[i], ApplyCurrentDayPose);
            }
        }
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
        Vector3 frontRight = SafeClassroomSpots[0];
        Vector3 frontLeft = SafeClassroomSpots[1];
        Vector3 backRight = SafeClassroomSpots[2];
        Vector3 backCenter = SafeClassroomSpots[3];
        Vector3 backLeft = SafeClassroomSpots[4];

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
                new DayPose { day = 5, localPosition = frontRight, localEulerAngles = faceFront, isActive = true }
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
