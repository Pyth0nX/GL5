using UnityEngine;

public class PhoneMessageManager : MonoBehaviour
{
    public PhoneConversation conversation;

    public Transform content;
    public GameObject messageBubblePrefab;

    void Start()
    {
        LoadConversation();
    }

    void LoadConversation()
    {
        foreach (PhoneMessage message in conversation.messages)
        {
            GameObject newBubble =
                Instantiate(messageBubblePrefab, content);

            PhoneMessageBubble bubble =
                newBubble.GetComponent<PhoneMessageBubble>();

            bubble.Setup(message);
        }
    }
}