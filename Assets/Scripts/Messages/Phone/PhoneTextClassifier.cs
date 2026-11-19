using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Analyzes free-text English player replies in Mode A phone conversations,
/// expanding common chat abbreviations and applying Levenshtein fuzzy matching
/// to classify the response into Safe (0), Neutral (1), or Risky (2).
/// </summary>
public static class PhoneTextClassifier
{
    public const int SafeRouteIndex = 0;
    public const int NeutralRouteIndex = 1;
    public const int RiskyRouteIndex = 2;

    private const int PhraseMatchWeight = 3;
    private const int KeywordMatchWeight = 2;
    private const int GeneralPatternWeight = 1;

    private static readonly Dictionary<string, string> AbbreviationMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "idk", "i do not know" },
        { "idc", "i do not care" },
        { "tbh", "to be honest" },
        { "tbf", "to be fair" },
        { "pls", "please" },
        { "plz", "please" },
        { "u", "you" },
        { "ur", "your" },
        { "r", "are" },
        { "im", "i am" },
        { "ive", "i have" },
        { "id", "i would" },
        { "ill", "i will" },
        { "youre", "you are" },
        { "youll", "you will" },
        { "dont", "do not" },
        { "doesnt", "does not" },
        { "didnt", "did not" },
        { "cant", "cannot" },
        { "wont", "will not" },
        { "shouldnt", "should not" },
        { "wouldnt", "would not" },
        { "couldnt", "could not" },
        { "isnt", "is not" },
        { "arent", "are not" },
        { "havent", "have not" },
        { "aint", "am not" },
        { "thats", "that is" },
        { "theres", "there is" },
        { "whats", "what is" },
        { "whos", "who is" },
        { "nvm", "never mind" },
        { "rn", "right now" },
        { "irl", "in real life" },
        { "ngl", "not gonna lie" },
        { "imo", "in my opinion" },
        { "bc", "because" },
        { "cuz", "because" },
        { "thx", "thank you" },
        { "ty", "thank you" },
        { "tysm", "thank you" },
        { "np", "no problem" },
        { "nah", "no" },
        { "nope", "no" },
        { "yea", "yes" },
        { "yeah", "yes" },
        { "yep", "yes" },
        { "yup", "yes" },
        { "sry", "sorry" },
        { "srry", "sorry" },
        { "wbu", "what about you" },
        { "hbu", "how about you" },
        { "pic", "picture" },
        { "pics", "picture" },
        { "addy", "address" },
        { "addr", "address" },
        { "info", "information" },
        { "priv", "private" },
        { "strngr", "stranger" },
        { "hmwk", "homework" },
        { "hw", "homework" },
        { "k", "okay" },
        { "kk", "okay" },
        { "ok", "okay" },
        { "msg", "message" },
        { "dm", "message" },
        { "txt", "text" },
        { "schl", "school" },
        { "ave", "avenue" },
        { "st", "street" },
        { "rd", "road" },
        { "ppl", "people" },
        { "ppls", "people" },
        { "b4", "before" },
        { "smth", "something" },
        { "nth", "nothing" },
        { "abt", "about" },
        { "rlly", "really" },
        { "srsly", "seriously" },
        { "def", "definitely" },
        { "prob", "probably" },
        { "ik", "i know" },
        { "ig", "instagram" },
        { "sc", "snapchat" },
        { "uncomfy", "uncomfortable" }
    };

    private static readonly string[] SafeGeneralPatterns =
    {
        "private", "privacy", "personal information", "none of your business", "not telling",
        "not sharing", "rather not", "prefer not", "do not share", "do not tell",
        "will not say", "will not share", "do not ask", "stop asking", "none of your concern",
        "cannot tell", "will not tell", "do not know you", "stranger", "leave me alone",
        "stop messaging", "stop texting", "keep it to myself", "keep that private", "block", "report",
        "boundary", "boundaries", "not comfortable", "uncomfortable", "no thanks",
        "no way", "trusted adult", "tell my parents", "ask my parents", "stay safe",
        "careful", "just about the book", "only books", "keep it private", "mind your business"
    };

    private static readonly string[] RiskyGeneralPatterns =
    {
        "rainfall", "maple avenue", "maple street", "cedar park", "my address",
        "where i live", "live near", "walk home", "walking home", "every day",
        "home alone", "parents are away", "parents are busy", "no one checks",
        "fifteen", "15 years old", "sophomore", "send you everything", "trust you",
        "here is my password", "my password", "my login", "my email", "account details",
        "shut up", "you idiot", "stupid", "hate you", "fight me",
        "do not care what happens", "leave me alone or else", "you are pathetic", "post whatever"
    };

    private static readonly HashSet<string> NegationTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "not", "no", "never", "refuse", "stop", "cannot", "avoid", "wont", "dont", "nope", "neither", "decline", "without"
    };

    private static readonly string[] SensitiveDisclosureTokens =
    {
        "school", "rainfall", "street", "avenue", "road", "park", "address", "home", "house",
        "live", "route", "walk", "gate", "bus", "age", "old", "grade", "sophomore", "fifteen", "15",
        "password", "login", "email", "account", "details", "selfie", "picture", "photo",
        "instagram", "snapchat", "location"
    };

    /// <summary>
    /// Classifies raw English text against the active Mode A NarrativeNode options:
    /// 0 = Safe, 1 = Neutral (default fallback), 2 = Risky.
    /// </summary>
    public static int ClassifyResponse(string rawInput, NarrativeNode node)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return NeutralRouteIndex;
        }

        List<string> inputTokens = TokenizeAndExpand(rawInput);
        if (inputTokens.Count == 0)
        {
            return NeutralRouteIndex;
        }

        int safeScore = 0;
        int neutralScore = 0;
        int riskyScore = 0;

        Option[] options = node != null ? node.Options() : null;
        if (options != null)
        {
            if (options.Length > SafeRouteIndex && options[SafeRouteIndex] != null)
            {
                safeScore += ScoreKeywords(inputTokens, options[SafeRouteIndex].matchKeywords, suppressIfNegated: false);
            }

            if (options.Length > NeutralRouteIndex && options[NeutralRouteIndex] != null)
            {
                neutralScore += ScoreKeywords(inputTokens, options[NeutralRouteIndex].matchKeywords, suppressIfNegated: false);
            }

            if (options.Length > RiskyRouteIndex && options[RiskyRouteIndex] != null)
            {
                riskyScore += ScoreKeywords(inputTokens, options[RiskyRouteIndex].matchKeywords, suppressIfNegated: true);
            }
        }

        safeScore += ScoreGeneralPatterns(inputTokens, SafeGeneralPatterns, suppressIfNegated: false);
        riskyScore += ScoreGeneralPatterns(inputTokens, RiskyGeneralPatterns, suppressIfNegated: true);

        // Negated mentions of sensitive concepts ("I won't tell you my school/address") reinforce Safe.
        if (ContainsNegatedSensitiveDisclosure(inputTokens))
        {
            safeScore += PhraseMatchWeight;
        }

        if (safeScore > riskyScore && safeScore >= neutralScore && safeScore > 0)
        {
            return SafeRouteIndex;
        }

        if (riskyScore > safeScore && riskyScore >= neutralScore && riskyScore > 0)
        {
            return RiskyRouteIndex;
        }

        return NeutralRouteIndex;
    }

    /// <summary>
    /// Computes the standard Levenshtein edit distance between two lowercase strings.
    /// </summary>
    public static int ComputeLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.IsNullOrEmpty(target) ? 0 : target.Length;
        }

        if (string.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        int n = source.Length;
        int m = target.Length;
        int[] previousRow = new int[m + 1];
        int[] currentRow = new int[m + 1];

        for (int j = 0; j <= m; j++)
        {
            previousRow[j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            currentRow[0] = i;
            char sourceChar = source[i - 1];

            for (int j = 1; j <= m; j++)
            {
                int cost = sourceChar == target[j - 1] ? 0 : 1;
                int insertCost = currentRow[j - 1] + 1;
                int deleteCost = previousRow[j] + 1;
                int replaceCost = previousRow[j - 1] + cost;
                currentRow[j] = Math.Min(Math.Min(insertCost, deleteCost), replaceCost);
            }

            int[] temp = previousRow;
            previousRow = currentRow;
            currentRow = temp;
        }

        return previousRow[m];
    }

    private static int ScoreKeywords(List<string> inputTokens, string[] keywords, bool suppressIfNegated)
    {
        if (keywords == null || keywords.Length == 0)
        {
            return 0;
        }

        int score = 0;
        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            List<string> patternTokens = TokenizeAndExpand(keyword);
            if (patternTokens.Count == 0)
            {
                continue;
            }

            int matchIndex = FindTokenPatternMatch(inputTokens, patternTokens);
            if (matchIndex >= 0 && (!suppressIfNegated || !IsTokenNegated(inputTokens, matchIndex)))
            {
                score += patternTokens.Count > 1 ? PhraseMatchWeight : KeywordMatchWeight;
            }
        }

        return score;
    }

    private static int ScoreGeneralPatterns(List<string> inputTokens, string[] patterns, bool suppressIfNegated)
    {
        int score = 0;
        for (int i = 0; i < patterns.Length; i++)
        {
            List<string> patternTokens = TokenizeAndExpand(patterns[i]);
            if (patternTokens.Count == 0)
            {
                continue;
            }

            int matchIndex = FindTokenPatternMatch(inputTokens, patternTokens);
            if (matchIndex >= 0 && (!suppressIfNegated || !IsTokenNegated(inputTokens, matchIndex)))
            {
                score += GeneralPatternWeight;
            }
        }

        return score;
    }

    private static bool ContainsNegatedSensitiveDisclosure(List<string> inputTokens)
    {
        for (int i = 0; i < inputTokens.Count; i++)
        {
            string token = inputTokens[i];
            bool isSensitive = false;
            for (int s = 0; s < SensitiveDisclosureTokens.Length; s++)
            {
                if (IsFuzzyTokenMatch(token, SensitiveDisclosureTokens[s]))
                {
                    isSensitive = true;
                    break;
                }
            }

            if (isSensitive && IsTokenNegated(inputTokens, i))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTokenNegated(List<string> inputTokens, int tokenIndex)
    {
        int lookbackStart = Math.Max(0, tokenIndex - 7);
        for (int j = tokenIndex - 1; j >= lookbackStart; j--)
        {
            string tok = inputTokens[j];
            if (tok == "but" || tok == "though" || tok == "however" || tok == "anyway")
            {
                break;
            }

            if (NegationTokens.Contains(tok))
            {
                if (tok == "no" && j + 1 < tokenIndex &&
                    (inputTokens[j + 1] == "problem" || inputTokens[j + 1] == "worries"))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static int FindTokenPatternMatch(List<string> inputTokens, List<string> patternTokens)
    {
        int patternCount = patternTokens.Count;
        if (patternCount == 0 || inputTokens.Count == 0)
        {
            return -1;
        }

        if (patternCount == 1)
        {
            string singlePattern = patternTokens[0];
            for (int i = 0; i < inputTokens.Count; i++)
            {
                if (IsFuzzyTokenMatch(inputTokens[i], singlePattern))
                {
                    return i;
                }
            }
            return -1;
        }

        if (inputTokens.Count >= patternCount)
        {
            int maxStart = inputTokens.Count - patternCount;
            for (int start = 0; start <= maxStart; start++)
            {
                bool allMatch = true;
                for (int offset = 0; offset < patternCount; offset++)
                {
                    if (!IsFuzzyTokenMatch(inputTokens[start + offset], patternTokens[offset]))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    return start;
                }
            }
        }

        // Fallback: allow matching phrases written without spaces (e.g. "rainfallschool").
        string joinedPattern = string.Concat(patternTokens);
        if (joinedPattern.Length >= 7)
        {
            for (int i = 0; i < inputTokens.Count; i++)
            {
                if (IsFuzzyTokenMatch(inputTokens[i], joinedPattern))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool IsFuzzyTokenMatch(string inputToken, string targetToken)
    {
        if (string.Equals(inputToken, targetToken, StringComparison.Ordinal))
        {
            return true;
        }

        int maxDistance = GetAllowedEditDistance(targetToken.Length);
        if (maxDistance == 0)
        {
            return false;
        }

        if (Math.Abs(inputToken.Length - targetToken.Length) > maxDistance)
        {
            return false;
        }

        return ComputeLevenshteinDistance(inputToken, targetToken) <= maxDistance;
    }

    private static int GetAllowedEditDistance(int wordLength)
    {
        if (wordLength <= 3) return 0;
        if (wordLength <= 6) return 1;
        return 2;
    }

    private static List<string> TokenizeAndExpand(string rawText)
    {
        List<string> rawTokens = ExtractCleanTokens(rawText);
        List<string> expandedTokens = new List<string>(rawTokens.Count * 2);

        for (int i = 0; i < rawTokens.Count; i++)
        {
            string token = rawTokens[i];
            if (AbbreviationMap.TryGetValue(token, out string expansion))
            {
                string[] parts = expansion.Split(' ');
                for (int p = 0; p < parts.Length; p++)
                {
                    if (!string.IsNullOrEmpty(parts[p]))
                    {
                        expandedTokens.Add(parts[p]);
                    }
                }
            }
            else
            {
                expandedTokens.Add(token);
            }
        }

        return expandedTokens;
    }

    private static List<string> ExtractCleanTokens(string rawText)
    {
        List<string> tokens = new List<string>();
        if (string.IsNullOrEmpty(rawText))
        {
            return tokens;
        }

        StringBuilder current = new StringBuilder();
        for (int i = 0; i < rawText.Length; i++)
        {
            char c = rawText[i];
            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
            }
            else if (c == '\'' || c == '\u2019')
            {
                // Strip apostrophes inside contractions ("don't" -> "dont") so they match AbbreviationMap.
                continue;
            }
            else
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
