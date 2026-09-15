using UnityEngine;

public class PhoneConversationTester : MonoBehaviour
{
    public PhoneConversation conversation;

    void Start()
    {
        foreach (PhoneMessage phoneMessage in conversation.messages)
        {
            Debug.Log(phoneMessage.sender + ": " + phoneMessage.message);
        }
    }
}