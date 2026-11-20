using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct CharacterDialogueStyle
{
    public string characterName;
    public Color characterBoxColor;
}

[Serializable]
public struct DialogueBoxStyle
{
    public Color boxColor;
    public Color characterBoxColor;
    public Color nameColor;
    public Color textColor;
    public bool italic;
}

/// <summary>
/// Reusable ScriptableObject palette for DialogueManager.
/// Allows configuring Narrator/Thought styles and per-NPC name-box colors in a single shared asset across scenes.
/// </summary>
[CreateAssetMenu(fileName = "DialogueVisualTheme", menuName = "Dialogue/Visual Theme")]
public class DialogueVisualTheme : ScriptableObject
{
    [Header("Special Speakers")]
    [SerializeField]
    private DialogueBoxStyle _narratorStyle = DialogueStyleResolver.DefaultNarratorStyle;

    [SerializeField]
    private DialogueBoxStyle _thoughtStyle = DialogueStyleResolver.DefaultThoughtStyle;

    [Header("In-Person NPC Name Box Colors")]
    [SerializeField]
    private List<CharacterDialogueStyle> _npcNameBoxColors = new();

    private Dictionary<string, Color> _lookupCache;

    public DialogueBoxStyle NarratorStyle => _narratorStyle;
    public DialogueBoxStyle ThoughtStyle => _thoughtStyle;

    private void OnEnable()
    {
        _lookupCache = null;
    }

    private void OnValidate()
    {
        _lookupCache = null;
    }

    public bool TryGetNameBoxColor(string speakerKey, out Color color)
    {
        if (string.IsNullOrWhiteSpace(speakerKey))
        {
            color = default;
            return false;
        }

        EnsureLookupCache();
        return _lookupCache.TryGetValue(speakerKey.Trim(), out color);
    }

    private void EnsureLookupCache()
    {
        if (_lookupCache != null)
        {
            return;
        }

        _lookupCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        if (_npcNameBoxColors == null)
        {
            return;
        }

        for (int i = 0; i < _npcNameBoxColors.Count; i++)
        {
            CharacterDialogueStyle entry = _npcNameBoxColors[i];
            if (!string.IsNullOrWhiteSpace(entry.characterName))
            {
                _lookupCache[entry.characterName.Trim()] = entry.characterBoxColor;
            }
        }
    }
}

/// <summary>
/// Resolves and applies DialogueManager visual styles cleanly outside DialogueManager's flow control.
/// Resolution priority for in-person speaker name-box colors:
/// 1. Scene overrides on DialogueManager
/// 2. Runtime per-NPC overrides registered by NPCData
/// 3. Assigned DialogueVisualTheme ScriptableObject asset
/// 4. Built-in default NPC color table (plus active player name alias)
/// 5. Deterministic HSV hash fallback for any unlisted/future speaker
/// </summary>
public sealed class DialogueStyleResolver
{
    public static readonly DialogueBoxStyle DefaultNarratorStyle = new DialogueBoxStyle
    {
        boxColor = new Color(0.08f, 0.11f, 0.20f, 0.90f),
        characterBoxColor = new Color(0.52f, 0.36f, 0.10f, 0.96f),
        nameColor = new Color(1f, 0.88f, 0.52f, 1f),
        textColor = new Color(0.97f, 0.93f, 0.80f, 1f),
        italic = true
    };

    public static readonly DialogueBoxStyle DefaultThoughtStyle = new DialogueBoxStyle
    {
        boxColor = new Color(0.14f, 0.09f, 0.24f, 0.90f),
        characterBoxColor = new Color(0.32f, 0.18f, 0.52f, 0.96f),
        nameColor = new Color(0.84f, 0.78f, 1f, 1f),
        textColor = new Color(0.90f, 0.88f, 1f, 1f),
        italic = true
    };

    private static readonly Dictionary<string, Color> BuiltInNpcNameBoxColors =
        new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["Emma"] = new Color(0.64f, 0.20f, 0.30f, 0.96f),
            ["Sophie"] = new Color(0.60f, 0.18f, 0.50f, 0.96f),
            ["Leo"] = new Color(0.15f, 0.50f, 0.26f, 0.96f),
            ["Chloe"] = new Color(0.12f, 0.48f, 0.60f, 0.96f),
            ["Liam"] = new Color(0.66f, 0.28f, 0.11f, 0.96f),
            ["Mia"] = new Color(0.58f, 0.45f, 0.10f, 0.96f),
            ["Ethan"] = new Color(0.17f, 0.34f, 0.68f, 0.96f),
            ["Mr. Henderson"] = new Color(0.38f, 0.36f, 0.24f, 0.96f),
            ["Bob"] = new Color(0.56f, 0.22f, 0.18f, 0.96f),
            ["Student 1"] = new Color(0.18f, 0.44f, 0.40f, 0.96f),
            ["Student 2"] = new Color(0.46f, 0.24f, 0.42f, 0.96f),
            ["Alex"] = new Color(0.14f, 0.54f, 0.40f, 0.96f),
            ["Mom"] = new Color(0.52f, 0.28f, 0.42f, 0.96f),
            ["Dad"] = new Color(0.36f, 0.31f, 0.22f, 0.96f),
            ["Maya"] = new Color(0.70f, 0.34f, 0.22f, 0.96f),
            ["Jamie"] = new Color(0.34f, 0.52f, 0.16f, 0.96f),
            ["Noah"] = new Color(0.22f, 0.28f, 0.64f, 0.96f),
            ["Riley"] = new Color(0.54f, 0.16f, 0.24f, 0.96f),
            ["Unknown"] = new Color(0.28f, 0.28f, 0.31f, 0.96f),
            ["Luna"] = new Color(0.22f, 0.40f, 0.62f, 0.96f)
        };

    private static readonly Dictionary<string, Color> RuntimeNpcColorOverrides =
        new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

    private Image _dialogueBoxImage;
    private Image _characterBoxImage;
    private TextMeshProUGUI _characterNameText;
    private TextMeshProUGUI _dialogueText;

    private bool _defaultsCaptured;
    private Color _defaultBoxColor = new Color(1f, 1f, 1f, 0.392f);
    private Color _defaultCharacterBoxColor = new Color(0f, 0f, 0f, 1f);
    private Color _defaultNameColor = Color.white;
    private Color _defaultDialogueTextColor = Color.white;

    private Dictionary<string, Color> _sceneOverrideCache;
    private List<CharacterDialogueStyle> _cachedOverrideSource;
    private int _cachedOverrideCount = -1;

    public static void RegisterRuntimeOverride(string characterName, Color color)
    {
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            RuntimeNpcColorOverrides[characterName.Trim()] = color;
        }
    }

    public static void UnregisterRuntimeOverride(string characterName)
    {
        if (!string.IsNullOrWhiteSpace(characterName))
        {
            RuntimeNpcColorOverrides.Remove(characterName.Trim());
        }
    }

    public void InvalidateSceneOverrideCache()
    {
        _sceneOverrideCache = null;
        _cachedOverrideCount = -1;
    }

    public void EnsureInitialized(
        GameObject dialogueBox,
        TextMeshProUGUI characterNameText,
        TextMeshProUGUI dialogueText)
    {
        if (_defaultsCaptured || dialogueBox == null)
        {
            return;
        }

        _characterNameText = characterNameText;
        _dialogueText = dialogueText;
        _dialogueBoxImage = dialogueBox.GetComponent<Image>();

        if (_characterNameText != null &&
            _characterNameText.transform.parent != null &&
            _characterNameText.transform.parent != dialogueBox.transform)
        {
            _characterBoxImage = _characterNameText.transform.parent.GetComponent<Image>();
        }

        if (_characterBoxImage == null)
        {
            Transform charBox = dialogueBox.transform.Find("BG_CharacterBox");
            if (charBox != null)
            {
                _characterBoxImage = charBox.GetComponent<Image>();
            }
        }

        if (_dialogueBoxImage != null) _defaultBoxColor = _dialogueBoxImage.color;
        if (_characterBoxImage != null) _defaultCharacterBoxColor = _characterBoxImage.color;
        if (_characterNameText != null) _defaultNameColor = _characterNameText.color;
        if (_dialogueText != null) _defaultDialogueTextColor = _dialogueText.color;

        _defaultsCaptured = true;
    }

    public void ApplyStyle(
        GameObject dialogueBox,
        TextMeshProUGUI characterNameText,
        TextMeshProUGUI dialogueText,
        string rawSpeaker,
        bool forceThought,
        DialogueVisualTheme theme,
        List<CharacterDialogueStyle> sceneOverrides)
    {
        EnsureInitialized(dialogueBox, characterNameText, dialogueText);

        string trimmedSpeaker = (rawSpeaker ?? string.Empty).Trim();
        bool isNarration = !forceThought && IsNarrationSpeaker(trimmedSpeaker);
        bool isThought = forceThought || IsThoughtSpeaker(trimmedSpeaker);

        if (isNarration)
        {
            DialogueBoxStyle style = theme != null ? theme.NarratorStyle : DefaultNarratorStyle;
            ApplyBoxStyle(style, "Narrator");
            return;
        }

        if (isThought)
        {
            DialogueBoxStyle style = theme != null ? theme.ThoughtStyle : DefaultThoughtStyle;
            ApplyBoxStyle(style, $"{PlayerNameService.GetActivePlayerName()} (Thought)");
            return;
        }

        // In-person dialogue: keep default box & text styling unchanged, varying only the speaker name box color.
        string formattedSpeaker = PlayerNameService.FormatText(rawSpeaker);
        Color nameBoxColor = ResolveInPersonNameBoxColor(formattedSpeaker, trimmedSpeaker, theme, sceneOverrides);
        DialogueBoxStyle inPersonStyle = new DialogueBoxStyle
        {
            boxColor = _defaultBoxColor,
            characterBoxColor = nameBoxColor,
            nameColor = _defaultNameColor,
            textColor = _defaultDialogueTextColor,
            italic = false
        };
        ApplyBoxStyle(inPersonStyle, formattedSpeaker);
    }

    private void ApplyBoxStyle(DialogueBoxStyle style, string displaySpeaker)
    {
        if (_dialogueBoxImage != null) _dialogueBoxImage.color = style.boxColor;
        if (_characterBoxImage != null) _characterBoxImage.color = style.characterBoxColor;

        if (_characterNameText != null)
        {
            _characterNameText.color = style.nameColor;
            _characterNameText.text = displaySpeaker;
        }

        if (_dialogueText != null)
        {
            _dialogueText.color = style.textColor;
            _dialogueText.fontStyle = style.italic ? FontStyles.Italic : FontStyles.Normal;
        }
    }

    private Color ResolveInPersonNameBoxColor(
        string formattedSpeaker,
        string rawTrimmedSpeaker,
        DialogueVisualTheme theme,
        List<CharacterDialogueStyle> sceneOverrides)
    {
        if (TryGetSceneOverride(formattedSpeaker, sceneOverrides, out Color color) ||
            TryGetSceneOverride(rawTrimmedSpeaker, sceneOverrides, out color))
        {
            return color;
        }

        if (TryGetFromDictionary(RuntimeNpcColorOverrides, formattedSpeaker, out color) ||
            TryGetFromDictionary(RuntimeNpcColorOverrides, rawTrimmedSpeaker, out color))
        {
            return color;
        }

        if (theme != null &&
            (theme.TryGetNameBoxColor(formattedSpeaker, out color) ||
             theme.TryGetNameBoxColor(rawTrimmedSpeaker, out color)))
        {
            return color;
        }

        if (TryGetFromDictionary(BuiltInNpcNameBoxColors, formattedSpeaker, out color) ||
            TryGetFromDictionary(BuiltInNpcNameBoxColors, rawTrimmedSpeaker, out color))
        {
            return color;
        }

        string activePlayerName = PlayerNameService.GetActivePlayerName();
        if (!string.IsNullOrEmpty(formattedSpeaker) &&
            !string.IsNullOrEmpty(activePlayerName) &&
            string.Equals(formattedSpeaker.Trim(), activePlayerName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            TryResolvePlayerDefaultColor(theme, sceneOverrides, out color))
        {
            return color;
        }

        string fallbackKey = !string.IsNullOrWhiteSpace(formattedSpeaker) ? formattedSpeaker.Trim() : rawTrimmedSpeaker;
        return string.IsNullOrEmpty(fallbackKey)
            ? _defaultCharacterBoxColor
            : GenerateDeterministicNameBoxColor(fallbackKey);
    }

    private bool TryResolvePlayerDefaultColor(
        DialogueVisualTheme theme,
        List<CharacterDialogueStyle> sceneOverrides,
        out Color color)
    {
        if (TryGetSceneOverride("Luna", sceneOverrides, out color)) return true;
        if (TryGetFromDictionary(RuntimeNpcColorOverrides, "Luna", out color)) return true;
        if (theme != null && theme.TryGetNameBoxColor("Luna", out color)) return true;
        return BuiltInNpcNameBoxColors.TryGetValue("Luna", out color);
    }

    private bool TryGetSceneOverride(
        string speakerKey,
        List<CharacterDialogueStyle> sceneOverrides,
        out Color color)
    {
        if (sceneOverrides == null || sceneOverrides.Count == 0 || string.IsNullOrWhiteSpace(speakerKey))
        {
            color = default;
            return false;
        }

        if (_sceneOverrideCache == null ||
            !ReferenceEquals(_cachedOverrideSource, sceneOverrides) ||
            _cachedOverrideCount != sceneOverrides.Count)
        {
            _sceneOverrideCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sceneOverrides.Count; i++)
            {
                CharacterDialogueStyle entry = sceneOverrides[i];
                if (!string.IsNullOrWhiteSpace(entry.characterName))
                {
                    _sceneOverrideCache[entry.characterName.Trim()] = entry.characterBoxColor;
                }
            }
            _cachedOverrideSource = sceneOverrides;
            _cachedOverrideCount = sceneOverrides.Count;
        }

        return _sceneOverrideCache.TryGetValue(speakerKey.Trim(), out color);
    }

    private static bool TryGetFromDictionary(Dictionary<string, Color> dict, string key, out Color color)
    {
        if (dict != null && dict.Count > 0 && !string.IsNullOrWhiteSpace(key))
        {
            return dict.TryGetValue(key.Trim(), out color);
        }

        color = default;
        return false;
    }

    private static bool IsNarrationSpeaker(string trimmedSpeaker)
    {
        return string.Equals(trimmedSpeaker, "Narration", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmedSpeaker, "Narrator", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThoughtSpeaker(string trimmedSpeaker)
    {
        return string.Equals(trimmedSpeaker, "Thought", StringComparison.OrdinalIgnoreCase) ||
               (trimmedSpeaker.StartsWith("[", StringComparison.Ordinal) &&
                trimmedSpeaker.EndsWith("]", StringComparison.Ordinal));
    }

    private static Color GenerateDeterministicNameBoxColor(string speakerKey)
    {
        uint hash = 2166136261u;
        string normalized = speakerKey.ToLowerInvariant();
        for (int i = 0; i < normalized.Length; i++)
        {
            hash ^= normalized[i];
            hash *= 16777619u;
        }

        float hue = (hash % 360u) / 360f;
        Color tagColor = Color.HSVToRGB(hue, 0.65f, 0.56f);
        tagColor.a = 0.96f;
        return tagColor;
    }
}
