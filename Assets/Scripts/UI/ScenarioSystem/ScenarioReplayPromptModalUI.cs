using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Post-campaign confirmation prompt displayed when closing the ScenarioSystem after completing
/// an ending, allowing the player to start a new playthrough in the same save slot, return to
/// Main Menu, or continue inspecting the flowchart.
/// </summary>
public class ScenarioReplayPromptModalUI : MonoBehaviour
{
    private UnityEngine.UI.Outline _cardOutline;
    private bool _isBuilt;

    private Action _onStartNewPlaythrough;
    private Action _onReturnToMainMenu;

    public bool IsOpen => gameObject != null && gameObject.activeSelf;

    /// <summary>
    /// Builds the prompt card hierarchy once and binds caller callbacks.
    /// </summary>
    public void EnsureBuilt(
        Transform parent,
        Color accentColor,
        Action onStartNewPlaythrough,
        Action onReturnToMainMenu)
    {
        if (parent != null && transform.parent != parent)
        {
            transform.SetParent(parent, false);
        }

        _onStartNewPlaythrough = onStartNewPlaythrough;
        _onReturnToMainMenu = onReturnToMainMenu;

        if (_cardOutline != null)
        {
            _cardOutline.effectColor = accentColor;
        }

        if (_isBuilt)
        {
            return;
        }

        ScenarioUiFactory.ConfigureFullScreenBackdrop(gameObject, ScenarioVisualTheme.BackdropColor);

        RectTransform cardRect = ScenarioUiFactory.CreateRoundedCard(
            transform,
            "PromptCard",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            ScenarioVisualTheme.ModalCardBgColor,
            accentColor,
            2f,
            out _,
            out _cardOutline);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(580f, 320f);

        CreateTitleText(cardRect);
        CreateBodyText(cardRect);

        CreateModalAction(
            cardRect,
            "StartNewPlaythroughButton",
            new Vector2(-175f, 48f),
            new Vector2(170f, 48f),
            "Start Over",
            new Color(0.16f, 0.52f, 0.36f, 1f),
            HandleStartNewPlaythroughClicked);

        CreateModalAction(
            cardRect,
            "ReturnToMenuButton",
            new Vector2(0f, 48f),
            new Vector2(160f, 48f),
            "Main Menu",
            new Color(0.48f, 0.14f, 0.16f, 1f),
            HandleMainMenuClicked);

        CreateModalAction(
            cardRect,
            "StayInScenarioButton",
            new Vector2(175f, 48f),
            new Vector2(160f, 48f),
            "Back to Tree",
            new Color(0.28f, 0.29f, 0.34f, 1f),
            Hide);

        _isBuilt = true;
        Hide();
    }

    public void Show()
    {
        if (gameObject == null) return;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (gameObject != null && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void HandleStartNewPlaythroughClicked()
    {
        Hide();
        _onStartNewPlaythrough?.Invoke();
    }

    private void HandleMainMenuClicked()
    {
        Hide();
        _onReturnToMainMenu?.Invoke();
    }

    private static void CreateTitleText(Transform cardParent)
    {
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(cardParent, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(-48f, 44f);

        TextMeshProUGUI tmp = titleObj.GetComponent<TextMeshProUGUI>();
        tmp.text = "Play Again?";
        tmp.fontSize = 28f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private static void CreateBodyText(Transform cardParent)
    {
        GameObject bodyObj = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObj.transform.SetParent(cardParent, false);
        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.36f);
        bodyRect.anchorMax = new Vector2(1f, 0.78f);
        bodyRect.offsetMin = new Vector2(36f, 0f);
        bodyRect.offsetMax = new Vector2(-36f, 0f);

        TextMeshProUGUI tmp = bodyObj.GetComponent<TextMeshProUGUI>();
        tmp.text =
            "Would you like to start a new playthrough?\n\n" +
            "Your reputation and story will reset to Day 1, while your previously discovered choices and endings remain saved in this slot.";
        tmp.fontSize = 18f;
        tmp.color = new Color(0.88f, 0.90f, 0.94f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
    }

    private static void CreateModalAction(
        Transform cardParent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        string label,
        Color bgColor,
        UnityEngine.Events.UnityAction onClick)
    {
        Button btn = ScenarioUiFactory.CreateRoundedButton(
            cardParent,
            name,
            label,
            bgColor,
            Color.white,
            16f,
            FontStyles.Bold,
            onClick,
            out _);

        RectTransform rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }
}
