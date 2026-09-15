using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewPhoneConversation",
    menuName = "Phone/Conversation"
)]
public class PhoneConversation : ScriptableObject
{
    public string conversationName;

    public List<PhoneMessage> messages = new List<PhoneMessage>();
}