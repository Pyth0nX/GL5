using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON-serializable representation of a 3D coordinate vector for Unity's JsonUtility.
/// </summary>
[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public SerializableVector3(Vector3 vector)
    {
        this.x = vector.x;
        this.y = vector.y;
        this.z = vector.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }

    public static implicit operator SerializableVector3(Vector3 vector)
    {
        return new SerializableVector3(vector);
    }

    public static implicit operator Vector3(SerializableVector3 serializableVector)
    {
        return serializableVector.ToVector3();
    }
}

/// <summary>
/// Associates a specific contact with its active narrative node and runtime phone conversation state.
/// </summary>
[Serializable]
public class ContactNarrativeSaveData
{
    [Tooltip("Unique name of the contact matching contacts.json.")]
    public string contactName;

    [Tooltip("Identifier of the current NarrativeNode assigned to this contact (-1 if none).")]
    public int currentNarrativeNodeId = -1;

    [Tooltip("Index of the next dialogue message to send within the active phone narrative node.")]
    public int currentMessageIndex = 0;

    [Tooltip("Indicates whether the contact has pending unread or interactive phone messages.")]
    public bool hasNewMessages = false;

    [Tooltip("Number of pending unread message notifications for this contact.")]
    public int unreadMessagesCount = 0;

    [Tooltip("Timestamp of the most recent message used to order contacts in the phone UI.")]
    public Date lastMessageDate;

    [Tooltip("Profile sprite path assigned to this contact.")]
    public string contactImagePath;

    [Tooltip("Complete phone chat message history exchanged with this contact.")]
    public List<Message> chatHistory = new List<Message>();
}

/// <summary>
/// Serializable snapshot of a runtime variable stored inside DecisionDataManager.
/// </summary>
[Serializable]
public struct DecisionVariableSaveData
{
    public string name;
    public DataType dataType;
    public string serializedValue;
    public string description;
}

/// <summary>
/// Serializable availability state for an IInteractable object inside a gameplay scene.
/// </summary>
[Serializable]
public struct SceneInteractableSaveData
{
    public string sceneName;
    public string hierarchyPath;
    public bool canInteract;
}

/// <summary>
/// Lightweight summary of a save slot used to populate Main Menu UI cards without loading gameplay state.
/// </summary>
[Serializable]
public class SaveSlotMetadata
{
    public int slotIndex;
    public bool isOccupied;
    public string playerName;
    public string saveTimestamp;
    public string currentObjective;
    public string sceneName;
    public int currentDay;
    public int reputationValue;
}

/// <summary>
/// Root serializable data model containing slot metadata, player location, reputation,
/// active objective, and per-contact narrative progression for a single save slot.
/// </summary>
[Serializable]
public class GameSaveData
{
    [Header("Slot Metadata")]
    [Tooltip("Zero-based index of the save slot (0, 1, or 2).")]
    public int slotIndex;

    [Tooltip("Formatted date and time when this save was written.")]
    public string saveTimestamp;

    [Tooltip("True when this slot contains valid saved gameplay data.")]
    public bool isOccupied;

    [Tooltip("Custom protagonist name chosen by the player for this save slot.")]
    public string playerName;

    [Tooltip("Display text of the active objective at the moment of saving.")]
    public string currentObjective;

    [Header("Player State")]
    [Tooltip("Name of the active gameplay scene where the player saved.")]
    public string sceneName;

    [Tooltip("Build index of the active gameplay scene.")]
    public int sceneBuildIndex;

    [Tooltip("3D world position of the player character.")]
    public SerializableVector3 playerPosition;

    [Tooltip("Horizontal yaw angle (euler Y) of the player character.")]
    public float playerRotationY;

    [Header("Reputation / Safety State")]
    [Tooltip("Current numeric Reputation / Safety value of the protagonist.")]
    public int reputationValue;

    [Tooltip("Cumulative narrative risk score tracked across Choices 1-13 for Day 5 ending resolution.")]
    public int narrativeRiskScore = 0;

    [Header("Narrative & Objective Progression")]
    [Tooltip("Current in-game day tracked by DayManager.")]
    public int currentDay = 1;

    [Tooltip("ID of the active Objective in ObjectivesManager.")]
    public int currentObjectiveId = 0;

    [Tooltip("Progress counter for multi-step objectives.")]
    public int currentObjectiveProgress = 0;

    [Tooltip("Whether the current objective is visible/active in the HUD.")]
    public bool isCurrentObjectiveActive = true;

    [Header("Connect App Progression")]
    [Tooltip("Whether Connect is unlocked for download in the App Store.")]
    public bool connectDownloadAvailable = false;

    [Tooltip("Whether Connect has been installed on Luna's phone.")]
    public bool connectInstalled = false;

    [Tooltip("Whether Luna has completed her Connect profile setup.")]
    public bool connectProfileCreated = false;

    [Tooltip("Serializable list mapping each contact to their current narrative node ID and chat history.")]
    public List<ContactNarrativeSaveData> contactNarrativeProgress = new List<ContactNarrativeSaveData>();

    [Tooltip("Serializable list of general decision variables registered in DecisionDataManager.")]
    public List<DecisionVariableSaveData> decisionVariables = new List<DecisionVariableSaveData>();

    [Tooltip("Paths or keys of photos unlocked in the phone Gallery.")]
    public List<string> unlockedGalleryPhotos = new List<string>();

    [Tooltip("Availability state of scene interactables (doors, bed, NPCs) across visited scenes.")]
    public List<SceneInteractableSaveData> sceneInteractables = new List<SceneInteractableSaveData>();

    /// <summary>
    /// Extracts lightweight metadata for UI display from this save data object.
    /// </summary>
    public SaveSlotMetadata ToMetadata()
    {
        return new SaveSlotMetadata
        {
            slotIndex = this.slotIndex,
            isOccupied = this.isOccupied,
            playerName = this.playerName,
            saveTimestamp = this.saveTimestamp,
            currentObjective = this.currentObjective,
            sceneName = this.sceneName,
            currentDay = this.currentDay,
            reputationValue = this.reputationValue
        };
    }
}
