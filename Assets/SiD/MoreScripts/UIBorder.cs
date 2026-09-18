using UnityEngine;
using UnityEngine.UI;

public class UIBorder : MonoBehaviour
{
    public Color borderColor = Color.white;
    public float thickness = 2f;

    private RectTransform rect;

    void Start()
    {
        rect = GetComponent<RectTransform>();

        CreateBorder("Top");
        CreateBorder("Bottom");
        CreateBorder("Left");
        CreateBorder("Right");
    }

    void CreateBorder(string side)
    {
        GameObject border = new GameObject(side);
        border.transform.SetParent(transform, false);

        Image image = border.AddComponent<Image>();
        image.color = borderColor;
        image.raycastTarget = false;

        RectTransform borderRect = border.GetComponent<RectTransform>();

        switch (side)
        {
            case "Top":
                borderRect.anchorMin = new Vector2(0, 1);
                borderRect.anchorMax = new Vector2(1, 1);
                borderRect.pivot = new Vector2(0.5f, 1);
                borderRect.sizeDelta = new Vector2(0, thickness);
                borderRect.anchoredPosition = Vector2.zero;
                break;

            case "Bottom":
                borderRect.anchorMin = new Vector2(0, 0);
                borderRect.anchorMax = new Vector2(1, 0);
                borderRect.pivot = new Vector2(0.5f, 0);
                borderRect.sizeDelta = new Vector2(0, thickness);
                borderRect.anchoredPosition = Vector2.zero;
                break;

            case "Left":
                borderRect.anchorMin = new Vector2(0, 0);
                borderRect.anchorMax = new Vector2(0, 1);
                borderRect.pivot = new Vector2(0, 0.5f);
                borderRect.sizeDelta = new Vector2(thickness, 0);
                borderRect.anchoredPosition = Vector2.zero;
                break;

            case "Right":
                borderRect.anchorMin = new Vector2(1, 0);
                borderRect.anchorMax = new Vector2(1, 1);
                borderRect.pivot = new Vector2(1, 0.5f);
                borderRect.sizeDelta = new Vector2(thickness, 0);
                borderRect.anchoredPosition = Vector2.zero;
                break;
        }
    }
}