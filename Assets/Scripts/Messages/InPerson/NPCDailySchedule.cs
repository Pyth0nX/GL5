using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Updates an NPC's local position, rotation, and active state based on the current story day
/// so classmates move around the classroom/school from day to day while never overlapping
/// the protagonist's desk or another classmate.
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
    private static readonly Vector3 ProtagonistDeskCenterLocalPos = new Vector3(-1.88f, 0f, -34.47f);
    private const float ProtagonistDeskExpandXZ = 2.2f;
    private const float StudentDeskExpandXZ = 0.7f;
    private const float SiblingWorldClearanceXZ = 1.8f;

    // Verified classroom student seat spots inside NPC parent bounds.
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

    private static int s_lastCoordinatedFrame = -1;
    private static int s_lastCoordinatedDay = -1;
    private static Scene s_lastCoordinatedScene;
    private static bool s_isCoordinating;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCoordinatorState()
    {
        s_lastCoordinatedFrame = -1;
        s_lastCoordinatedDay = -1;
        s_lastCoordinatedScene = default;
        s_isCoordinating = false;
    }

    [SerializeField] private List<DayPose> _dayPoses = new List<DayPose>();

    private bool _capturedDefault;
    private Vector3 _defaultLocalPos;
    private Quaternion _defaultLocalRot;
    private int _lastAppliedDay = -1;

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
        if (s_isCoordinating)
        {
            return;
        }

        CoordinateSceneSchedules(this);
    }

    private static void CoordinateSceneSchedules(NPCDailySchedule requester)
    {
        if (requester == null || s_isCoordinating)
        {
            return;
        }

        int day = DayManager.Instance != null ? Mathf.Max(1, DayManager.Instance.GetCurrentDay()) : 1;
        Scene targetScene = requester.gameObject.scene;

        // Avoid redundant full-scene passes in the exact same frame/day once already coordinated,
        // unless this instance hasn't applied its pose yet (handled by running when frame/day/scene changed
        // or always re-validating safely without duplicating reserved spots).
        bool alreadyCoordinatedThisFrame =
            s_lastCoordinatedFrame == Time.frameCount &&
            s_lastCoordinatedDay == day &&
            s_lastCoordinatedScene == targetScene;

        if (alreadyCoordinatedThisFrame && requester._lastAppliedDay == day)
        {
            return;
        }

        s_isCoordinating = true;
        try
        {
            NPCDailySchedule[] allFound = FindObjectsByType<NPCDailySchedule>(FindObjectsInactive.Include);
            List<NPCDailySchedule> sceneSchedules = new List<NPCDailySchedule>(allFound.Length);
            for (int i = 0; i < allFound.Length; i++)
            {
                NPCDailySchedule sched = allFound[i];
                if (sched != null && sched.gameObject.scene == targetScene)
                {
                    sceneSchedules.Add(sched);
                }
            }

            if (sceneSchedules.Count == 0)
            {
                return;
            }

            sceneSchedules.Sort((a, b) =>
            {
                int siblingCmp = a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
                if (siblingCmp != 0) return siblingCmp;
                return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
            });

            if (alreadyCoordinatedThisFrame)
            {
                // Still verify requester safely against already-placed siblings.
                sceneSchedules.Remove(requester);
                sceneSchedules.Add(requester);
            }

            CollectForbiddenWorldBounds(
                targetScene,
                requester,
                out List<Bounds> protagonistForbiddenBounds,
                out List<Bounds> studentDeskBounds);

            List<Vector3> reservedWorldPositions = new List<Vector3>(sceneSchedules.Count);

            for (int i = 0; i < sceneSchedules.Count; i++)
            {
                NPCDailySchedule sched = sceneSchedules[i];
                if (sched == null) continue;

                sched.CaptureDefaultPose();
                sched.EnsureDefaultScheduleIfEmpty();
                if (sched._dayPoses.Count == 0)
                {
                    continue;
                }

                DayPose pose = sched.SelectPoseForDay(day);

                if (sched.gameObject.activeSelf != pose.isActive)
                {
                    sched.gameObject.SetActive(pose.isActive);
                }

                Vector3 safeWorldPos = sched.ResolveSafeWorldPosition(
                    pose.localPosition,
                    day,
                    protagonistForbiddenBounds,
                    studentDeskBounds,
                    reservedWorldPositions);

                sched.transform.localPosition = sched.WorldToLocal(safeWorldPos);
                sched.transform.localRotation = Quaternion.Euler(pose.localEulerAngles);
                sched._lastAppliedDay = day;

                if (pose.isActive)
                {
                    reservedWorldPositions.Add(safeWorldPos);
                }
            }

            s_lastCoordinatedFrame = Time.frameCount;
            s_lastCoordinatedDay = day;
            s_lastCoordinatedScene = targetScene;
        }
        finally
        {
            s_isCoordinating = false;
        }
    }

    private DayPose SelectPoseForDay(int day)
    {
        for (int i = 0; i < _dayPoses.Count; i++)
        {
            if (_dayPoses[i].day == day)
            {
                return _dayPoses[i];
            }
        }

        return _dayPoses[(day - 1) % _dayPoses.Count];
    }

    private Vector3 ResolveSafeWorldPosition(
        Vector3 desiredLocalPos,
        int day,
        List<Bounds> protagonistForbiddenBounds,
        List<Bounds> studentDeskBounds,
        List<Vector3> reservedWorldPositions)
    {
        Vector3 desiredWorld = LocalToWorld(desiredLocalPos);
        if (IsWorldSpotSafe(desiredWorld, protagonistForbiddenBounds, studentDeskBounds, reservedWorldPositions))
        {
            return desiredWorld;
        }

        // Prefer original scene spawn if it clears the protagonist's desk, student tabletops, and other NPCs.
        Vector3 defaultWorld = LocalToWorld(_defaultLocalPos);
        if (IsWorldSpotSafe(defaultWorld, protagonistForbiddenBounds, studentDeskBounds, reservedWorldPositions))
        {
            Debug.LogWarning($"[NPCDailySchedule] '{gameObject.name}' Day {day} pose {desiredLocalPos} conflicted with desk/sibling bounds; redirected to default spot {_defaultLocalPos}.");
            return defaultWorld;
        }

        // First try verified student seats avoiding both protagonist desk and student desk meshes.
        for (int i = 0; i < SafeClassroomSpots.Length; i++)
        {
            Vector3 candidateWorld = LocalToWorld(SafeClassroomSpots[i]);
            if (IsWorldSpotSafe(candidateWorld, protagonistForbiddenBounds, studentDeskBounds, reservedWorldPositions))
            {
                Debug.LogWarning($"[NPCDailySchedule] '{gameObject.name}' Day {day} pose {desiredLocalPos} conflicted with desk/sibling bounds; redirected to {SafeClassroomSpots[i]}.");
                return candidateWorld;
            }
        }

        // Second pass: allow seat even if StudentDesk mesh bounds overlap slightly, but NEVER protagonist's desk or siblings.
        for (int i = 0; i < SafeClassroomSpots.Length; i++)
        {
            Vector3 candidateWorld = LocalToWorld(SafeClassroomSpots[i]);
            if (IsWorldSpotSafe(candidateWorld, protagonistForbiddenBounds, null, reservedWorldPositions))
            {
                Debug.LogWarning($"[NPCDailySchedule] '{gameObject.name}' Day {day} pose {desiredLocalPos} conflicted with protagonist desk; redirected to {SafeClassroomSpots[i]}.");
                return candidateWorld;
            }
        }

        // Final fallback: pick the safe classroom spot furthest from the protagonist's desk bounds.
        Vector3 bestWorld = LocalToWorld(SafeClassroomSpots[0]);
        float bestDistSq = -1f;
        for (int i = 0; i < SafeClassroomSpots.Length; i++)
        {
            Vector3 candidateWorld = LocalToWorld(SafeClassroomSpots[i]);
            if (IsInsideAnyBoundsXZ(candidateWorld, protagonistForbiddenBounds))
            {
                continue;
            }

            float distSq = MinimumDistanceSqToBoundsXZ(candidateWorld, protagonistForbiddenBounds);
            if (distSq > bestDistSq)
            {
                bestDistSq = distSq;
                bestWorld = candidateWorld;
            }
        }

        Debug.LogWarning($"[NPCDailySchedule] '{gameObject.name}' Day {day} pose {desiredLocalPos} overlapped forbidden area; redirected to fallback world spot {bestWorld}.");
        return bestWorld;
    }

    private Vector3 LocalToWorld(Vector3 localPos)
    {
        return transform.parent != null ? transform.parent.TransformPoint(localPos) : localPos;
    }

    private Vector3 WorldToLocal(Vector3 worldPos)
    {
        return transform.parent != null ? transform.parent.InverseTransformPoint(worldPos) : worldPos;
    }

    private static void CollectForbiddenWorldBounds(
        Scene scene,
        NPCDailySchedule referenceSchedule,
        out List<Bounds> protagonistForbiddenBounds,
        out List<Bounds> studentDeskBounds)
    {
        protagonistForbiddenBounds = new List<Bounds>();
        studentDeskBounds = new List<Bounds>();

        ClassDesk[] desks = FindObjectsByType<ClassDesk>(FindObjectsInactive.Include);
        for (int i = 0; i < desks.Length; i++)
        {
            ClassDesk desk = desks[i];
            if (desk == null || desk.gameObject.scene != scene) continue;

            Bounds deskZone = new Bounds(desk.transform.position, Vector3.zero);
            Collider[] cols = desk.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < cols.Length; c++)
            {
                if (cols[c] != null && cols[c].enabled)
                {
                    deskZone.Encapsulate(cols[c].bounds);
                }
            }

            Renderer[] rends = desk.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rends.Length; r++)
            {
                if (rends[r] != null && rends[r].enabled)
                {
                    deskZone.Encapsulate(rends[r].bounds);
                }
            }

            // Include Luna's seating area right behind LunaDesk.
            deskZone.Encapsulate(desk.transform.position + new Vector3(2.71f, 0f, 0.08f));
            deskZone.Expand(new Vector3(ProtagonistDeskExpandXZ, 6f, ProtagonistDeskExpandXZ));
            protagonistForbiddenBounds.Add(deskZone);
        }

        // Reference-parent fallback bounds around ProtagonistDeskLocalPos & tabletop center.
        if (referenceSchedule != null)
        {
            Vector3 fallbackSeatWorld = referenceSchedule.LocalToWorld(ProtagonistDeskLocalPos);
            Vector3 fallbackDeskWorld = referenceSchedule.LocalToWorld(ProtagonistDeskCenterLocalPos);
            Bounds fallbackZone = new Bounds(fallbackSeatWorld, new Vector3(2.2f, 6f, 2.2f));
            fallbackZone.Encapsulate(new Bounds(fallbackDeskWorld, new Vector3(2.4f, 6f, 2.4f)));
            protagonistForbiddenBounds.Add(fallbackZone);
        }

        MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            if (mr == null || mr.gameObject.scene != scene) continue;
            if (mr.gameObject.name.StartsWith("StudentDesk", StringComparison.OrdinalIgnoreCase))
            {
                Bounds b = mr.bounds;
                b.Expand(new Vector3(StudentDeskExpandXZ, 4f, StudentDeskExpandXZ));
                studentDeskBounds.Add(b);
            }
        }
    }

    private static bool IsWorldSpotSafe(
        Vector3 worldPos,
        List<Bounds> protagonistForbiddenBounds,
        List<Bounds> studentDeskBounds,
        List<Vector3> reservedWorldPositions)
    {
        if (IsInsideAnyBoundsXZ(worldPos, protagonistForbiddenBounds))
        {
            return false;
        }

        if (studentDeskBounds != null && IsInsideAnyBoundsXZ(worldPos, studentDeskBounds))
        {
            return false;
        }

        if (reservedWorldPositions != null)
        {
            float minSq = SiblingWorldClearanceXZ * SiblingWorldClearanceXZ;
            for (int i = 0; i < reservedWorldPositions.Count; i++)
            {
                Vector3 other = reservedWorldPositions[i];
                float dx = worldPos.x - other.x;
                float dz = worldPos.z - other.z;
                if ((dx * dx + dz * dz) < minSq)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsInsideAnyBoundsXZ(Vector3 worldPos, List<Bounds> boundsList)
    {
        if (boundsList == null) return false;

        for (int i = 0; i < boundsList.Count; i++)
        {
            Bounds b = boundsList[i];
            if (worldPos.x >= b.min.x && worldPos.x <= b.max.x &&
                worldPos.z >= b.min.z && worldPos.z <= b.max.z)
            {
                return true;
            }
        }

        return false;
    }

    private static float MinimumDistanceSqToBoundsXZ(Vector3 worldPos, List<Bounds> boundsList)
    {
        if (boundsList == null || boundsList.Count == 0) return float.MaxValue;

        float minSq = float.MaxValue;
        for (int i = 0; i < boundsList.Count; i++)
        {
            Bounds b = boundsList[i];
            float dx = worldPos.x - b.center.x;
            float dz = worldPos.z - b.center.z;
            float sq = dx * dx + dz * dz;
            if (sq < minSq)
            {
                minSq = sq;
            }
        }

        return minSq;
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
        Vector3 spot0 = SafeClassroomSpots[0]; // Front right
        Vector3 spot1 = SafeClassroomSpots[1]; // Front left
        Vector3 spot2 = SafeClassroomSpots[2]; // Back right
        Vector3 spot3 = SafeClassroomSpots[3]; // Back center
        Vector3 spot4 = SafeClassroomSpots[4]; // Back left

        Vector3 faceBoard = new Vector3(0f, 90f, 0f);
        Vector3 faceBoardLeft = new Vector3(0f, 102f, 0f);
        Vector3 faceBoardRight = new Vector3(0f, 78f, 0f);

        // Disjoint 5-seat Latin-square rotation across Days 1-5 so no two classmates share a desk
        // and no NPC ever uses the protagonist's front-center desk (0.83, 0, -34.39).
        return objName switch
        {
            "NPC (5)" or "NPC(5)" => new List<DayPose> // Leo
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = spot3, localEulerAngles = faceBoard, isActive = true },
                new DayPose { day = 3, localPosition = spot1, localEulerAngles = faceBoardRight, isActive = true },
                new DayPose { day = 4, localPosition = spot2, localEulerAngles = faceBoardLeft, isActive = true },
                new DayPose { day = 5, localPosition = spot4, localEulerAngles = faceBoardRight, isActive = true }
            },
            "NPC (6)" or "NPC(6)" => new List<DayPose> // Chloe
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = spot4, localEulerAngles = faceBoardRight, isActive = true },
                new DayPose { day = 3, localPosition = spot0, localEulerAngles = faceBoardLeft, isActive = true },
                new DayPose { day = 4, localPosition = spot1, localEulerAngles = faceBoardRight, isActive = true },
                new DayPose { day = 5, localPosition = spot3, localEulerAngles = faceBoard, isActive = true }
            },
            "NPC (8)" or "NPC(8)" => new List<DayPose> // Liam
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = spot1, localEulerAngles = faceBoardRight, isActive = true },
                new DayPose { day = 3, localPosition = spot2, localEulerAngles = faceBoardLeft, isActive = true },
                new DayPose { day = 4, localPosition = spot4, localEulerAngles = faceBoardRight, isActive = true },
                new DayPose { day = 5, localPosition = spot2, localEulerAngles = faceBoardLeft, isActive = true }
            },
            "NPC (9)" or "NPC(9)" => new List<DayPose> // Mia
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = spot2, localEulerAngles = faceBoardLeft, isActive = true },
                new DayPose { day = 3, localPosition = spot4, localEulerAngles = faceBoardRight, isActive = true },
                new DayPose { day = 4, localPosition = spot3, localEulerAngles = faceBoard, isActive = true },
                new DayPose { day = 5, localPosition = spot0, localEulerAngles = faceBoardLeft, isActive = true }
            },
            "NPC (10)" or "NPC(10)" => new List<DayPose> // Ethan
            {
                new DayPose { day = 1, localPosition = day1Pos, localEulerAngles = day1Euler, isActive = true },
                new DayPose { day = 2, localPosition = spot0, localEulerAngles = faceBoardLeft, isActive = true },
                new DayPose { day = 3, localPosition = spot3, localEulerAngles = faceBoard, isActive = true },
                new DayPose { day = 4, localPosition = spot0, localEulerAngles = faceBoardLeft, isActive = true },
                new DayPose { day = 5, localPosition = spot1, localEulerAngles = faceBoardRight, isActive = true }
            },
            _ => new List<DayPose>()
        };
    }
}
