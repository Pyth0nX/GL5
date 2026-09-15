using TMPro;
using UnityEngine;

public class PhoneMessageBubble : MonoBehaviour
{
    public TMP_Text senderText;
    public TMP_Text messageText;

    public void Setup(PhoneMessage message)
    {
        senderText.text = message.sender;
        messageText.text = message.message;
    }
}