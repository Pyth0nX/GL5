using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeightFallow : MonoBehaviour
{
    public RectTransform textBox;
    public float extraHeight = 10f;
    public float minHeight = 0f;

    private RectTransform imageBox;
    private LayoutElement layoutElement;
    private TextMeshProUGUI textMeshPro;

    void Awake()
    {
        imageBox = GetComponent<RectTransform>();
        layoutElement = GetComponent<LayoutElement>();
        if (textBox != null)
        {
            textMeshPro = textBox.GetComponent<TextMeshProUGUI>();
        }
        if (textMeshPro == null)
        {
            textMeshPro = GetComponentInChildren<TextMeshProUGUI>();
            if (textMeshPro != null && textBox == null)
            {
                textBox = textMeshPro.rectTransform;
            }
        }
    }

    void LateUpdate()
    {
        if (imageBox == null) return;

        float textHeight = 0f;
        if (textMeshPro != null)
        {
            textHeight = textMeshPro.preferredHeight;
        }
        else if (textBox != null)
        {
            textHeight = textBox.rect.height;
        }
        else
        {
            return;
        }

        float targetHeight = Mathf.Max(minHeight, textHeight + extraHeight);

        // Follow the text height
        if (Mathf.Abs(imageBox.rect.height - targetHeight) > 0.1f)
        {
            imageBox.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                targetHeight
            );

            if (layoutElement != null)
            {
                layoutElement.preferredHeight = targetHeight;
            }
        }

        // Follow the center position of the text only if textBox is not a child of imageBox
        if (textBox != null && !textBox.IsChildOf(imageBox))
        {
            imageBox.position = textBox.TransformPoint(textBox.rect.center);
        }
    }
}
