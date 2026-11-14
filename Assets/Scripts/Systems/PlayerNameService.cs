using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Centralized service responsible for storing, validating, and formatting the protagonist's name
/// across dialogues, phone messages, objectives, UI prompts, and save slot metadata.
/// Supports both explicit placeholders ({playerName}) and legacy protagonist references ("Luna").
/// </summary>
public static class PlayerNameService
{
    public const string DefaultPlayerName = "Luna";
    public const string PlaceholderToken = "{playerName}";
    public const int MaxNameLength = 20;

    private static readonly Regex LegacyNameRegex =
        new Regex(@"\bLuna\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RichTextTagStripRegex =
        new Regex(@"<[^>]*>", RegexOptions.Compiled);

    /// <summary>
    /// Returns the currently active protagonist name, falling back to DefaultPlayerName when unset.
    /// </summary>
    public static string GetActivePlayerName()
    {
        if (GameManager.Instance != null)
        {
            string storedName = GameManager.Instance.GetPlayerName();
            if (!string.IsNullOrWhiteSpace(storedName))
            {
                return storedName;
            }
        }

        return DefaultPlayerName;
    }

    /// <summary>
    /// Validates and cleans a user-provided protagonist name.
    /// Returns DefaultPlayerName if the sanitized input is empty.
    /// </summary>
    public static string SanitizeName(string rawName)
    {
        string cleaned = CleanRawName(rawName);
        return string.IsNullOrEmpty(cleaned) ? DefaultPlayerName : cleaned;
    }

    /// <summary>
    /// Attempts to sanitize a user-entered name without applying a default fallback.
    /// Returns true if at least one valid character remains after sanitization.
    /// </summary>
    public static bool TryCleanUserInput(string rawName, out string cleanedName)
    {
        cleanedName = CleanRawName(rawName);
        return !string.IsNullOrEmpty(cleanedName);
    }

    /// <summary>
    /// Formats any player-facing text by replacing {playerName} placeholders and standalone "Luna"
    /// references with the currently active protagonist name.
    /// </summary>
    public static string FormatText(string text)
    {
        return FormatText(text, GetActivePlayerName());
    }

    /// <summary>
    /// Formats any player-facing text using the specified protagonist name.
    /// </summary>
    public static string FormatText(string text, string playerName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string resolvedName = SanitizeName(playerName);
        string result = text;

        if (result.IndexOf(PlaceholderToken, System.StringComparison.Ordinal) >= 0)
        {
            result = result.Replace(PlaceholderToken, resolvedName);
        }

        if (!string.Equals(resolvedName, DefaultPlayerName, System.StringComparison.Ordinal) &&
            result.IndexOf(DefaultPlayerName, System.StringComparison.Ordinal) >= 0)
        {
            // Use MatchEvaluator so '$' characters inside a custom name are never treated as regex substitutions.
            result = LegacyNameRegex.Replace(result, _ => resolvedName);
        }

        return result;
    }

    /// <summary>
    /// Resolves the display speaker name for dialogue lines.
    /// </summary>
    public static string ResolveSpeakerName(string rawSpeaker, Sender sender)
    {
        if (sender == Sender.Player)
        {
            return GetActivePlayerName();
        }

        return FormatText(rawSpeaker);
    }

    private static string CleanRawName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        // Strip rich-text tags and angle brackets so user input cannot inject TMP markup.
        string noTags = RichTextTagStripRegex.Replace(rawName, string.Empty);
        StringBuilder sb = new StringBuilder(noTags.Length);
        bool previousWasSpace = false;

        for (int i = 0; i < noTags.Length; i++)
        {
            char c = noTags[i];
            if (c == '<' || c == '>' || char.IsControl(c))
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0 && !previousWasSpace)
                {
                    sb.Append(' ');
                    previousWasSpace = true;
                }
                continue;
            }

            sb.Append(c);
            previousWasSpace = false;
        }

        string normalized = sb.ToString().Trim();
        if (normalized.Length > MaxNameLength)
        {
            normalized = normalized.Substring(0, MaxNameLength).TrimEnd();
        }

        return normalized;
    }
}
