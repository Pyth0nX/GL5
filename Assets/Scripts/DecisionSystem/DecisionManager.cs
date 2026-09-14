using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;


// Struct to hold decision data, including the type of decision and its associated value
[System.Serializable] // Serializable to allow it to be displayed in the Inspector
public struct DecisionData
{
    public DecisionStat type;
    public int value;   
}


// Manager dedicated to save all data stats 
public class DecisionManager : MonoBehaviour
{
    public static DecisionManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); //Persist across scenes
        }
        else Destroy(gameObject); // Ensure only one instance exists

    }

    [SerializeField]
    private List<DecisionData> decisionDataList = new List<DecisionData>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public int GetDecisionValue(DecisionStat decisionType)
    {
        foreach (var decisionData in decisionDataList)
        {
            if (decisionData.type == decisionType)
            {
                return decisionData.value;
            }
        }
        return 0; // Return 0 if the decision type is not found
    }


    public void AddDecisionValue(DecisionStat decisionType, int valueToAdd)
    {
        for (int i = 0; i < decisionDataList.Count; i++)
        {
            if (decisionDataList[i].type == decisionType)
            {
                decisionDataList[i] = new DecisionData
                {
                    type = decisionType,
                    value = decisionDataList[i].value + valueToAdd
                };
            }
        }

        // If the decision type is not found, add a new entry
        DecisionData newDecisionData = new DecisionData
        {
            type = decisionType,
            value = valueToAdd
        };
        decisionDataList.Add(newDecisionData);
    }
}
