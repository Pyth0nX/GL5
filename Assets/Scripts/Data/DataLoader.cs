using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class DataLoader
{
    /// <summary>
    /// Load the complete list of contacts from a JSON file.
    /// </summary>
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
        List<NarrativeNode> nodes = wrapper != null && wrapper.narrativeNodes != null
            ? wrapper.narrativeNodes
            : new List<NarrativeNode>();

        EnsureChoiceReputationLinks(nodes);
        return nodes;
    }

    /// <summary>
    /// Automatically links narrative Choice*_Safe and Choice*_Risky options with ReputationManager
    /// when no explicit reputationOperation was serialized in JSON, avoiding double-counting.
    /// </summary>
    private static void EnsureChoiceReputationLinks(List<NarrativeNode> nodes)
    {
        if (nodes == null) return;

        const int defaultChoiceReputationDelta = 6;

        for (int i = 0; i < nodes.Count; i++)
        {
            NarrativeNode node = nodes[i];
            if (node == null || node.Options() == null) continue;

            Option[] options = node.Options();
            for (int j = 0; j < options.Length; j++)
            {
                Option opt = options[j];
                if (opt == null || opt.reputationOperation != ReputationOperation.None) continue;
                if (string.IsNullOrEmpty(opt.eventToPublish)) continue;

                if (opt.eventToPublish.StartsWith("Choice", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (opt.eventToPublish.EndsWith("_Safe", System.StringComparison.OrdinalIgnoreCase))
                    {
                        opt.reputationOperation = ReputationOperation.Add;
                        opt.reputationAmount = defaultChoiceReputationDelta;
                    }
                    else if (opt.eventToPublish.EndsWith("_Risky", System.StringComparison.OrdinalIgnoreCase))
                    {
                        opt.reputationOperation = ReputationOperation.Subtract;
                        opt.reputationAmount = defaultChoiceReputationDelta;
                    }
                }
            }
        }
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
