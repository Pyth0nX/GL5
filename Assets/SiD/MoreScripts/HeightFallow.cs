using UnityEngine;

public class HeightFallow : MonoBehaviour
{
    public RectTransform textBox;
    public float extraHeight = 10f;

    private RectTransform imageBox;

    void Awake()
    {
        imageBox = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        // Follow the text height
        imageBox.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            textBox.rect.height + extraHeight
        );

        // Follow the center position of the text
        imageBox.position = textBox.TransformPoint(textBox.rect.center);
    }
}