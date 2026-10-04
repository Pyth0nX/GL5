using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class DataLoader
{

    /// <summary>
    /// Load the complete list of contacts from a JSON file
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static List<Contact> LoadContacts(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        // Check if the file exists before attempting to read it
        if (!File.Exists(path))
        {
            // Returns an empty list if the file does not exist
            return new List<Contact>();
        }

        string json = File.ReadAllText(path);

        ContactListWrapper wrapper = JsonUtility.FromJson<ContactListWrapper>(json);

        return wrapper != null ? wrapper.contacts : new List<Contact>();
    }

    public static List<ContactPhoneMessages> LoadPhoneMessages(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        // Check if the file exists before attempting to read it
        if (!File.Exists(path))
        {
            // Returns an empty list if the file does not exist
            return new List<ContactPhoneMessages>();
        }

        string json = File.ReadAllText(path);
        PhoneMessageListWrapper wrapper = JsonUtility.FromJson<PhoneMessageListWrapper>(json);
        return wrapper != null ? wrapper.contactPhoneMessages : new List<ContactPhoneMessages>();
    }

    public static List<NarrativeNode> LoadNarrativeNodes(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        // Check if the file exists before attempting to read it
        if (!File.Exists(path))
        {
            // Returns an empty list if the file does not exist
            return new List<NarrativeNode>();
        }

        string json = File.ReadAllText(path);
        NarrativeNodeListWrapper wrapper = JsonUtility.FromJson<NarrativeNodeListWrapper>(json);
        return wrapper != null ? wrapper.narrativeNodes : new List<NarrativeNode>();
    }

    public static List<Objective> LoadObjectives(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            return new List<Objective>();
        }

        string json = File.ReadAllText(path);
        ObjectiveListWrapper wrapper = JsonUtility.FromJson<ObjectiveListWrapper>(json);
        return wrapper != null ? wrapper.objectives : new List<Objective>();
    }
}
