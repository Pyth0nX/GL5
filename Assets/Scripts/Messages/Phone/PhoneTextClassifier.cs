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
    private const int StackAllocIntThreshold = 256;
    private const int PatternCacheLimit = 32768;

    private readonly struct CompiledPattern
    {
        public readonly string[] Tokens;
        public readonly string JoinedCompact;
        public readonly int KeywordWeight;

        public CompiledPattern(string[] tokens, string joinedCompact, int keywordWeight)
        {
            Tokens = tokens;
            JoinedCompact = joinedCompact;
            KeywordWeight = keywordWeight;
        }
    }

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
        { "uncomfy", "uncomfortable" },
        { "gonna", "going to" },
        { "wanna", "want to" },
        { "gotta", "got to" },
        { "lemme", "let me" },
        { "gimme", "give me" },
        { "dunno", "do not know" },
        { "kinda", "kind of" },
        { "sorta", "sort of" },
        { "outta", "out of" },
        { "insta", "instagram" },
        { "snap", "snapchat" },
        { "tt", "tiktok" },
        { "fb", "facebook" },
        { "pfp", "profile picture" },
        { "acc", "account" },
        { "acct", "account" },
        { "anon", "anonymous" },
        { "ss", "screenshot" },
        { "omg", "oh my god" },
        { "fr", "for real" },
        { "ikr", "i know right" },
        { "wdym", "what do you mean" },
        { "wym", "what do you mean" },
        { "wyd", "what are you doing" },
        { "hmu", "hit me up" },
        { "brb", "be right back" },
        { "gtg", "got to go" },
        { "g2g", "got to go" },
        { "omw", "on my way" },
        { "prolly", "probably" },
        { "defo", "definitely" },
        { "obv", "obviously" },
        { "obvi", "obviously" },
        { "tho", "though" },
        { "thru", "through" },
        { "tmrw", "tomorrow" },
        { "tmr", "tomorrow" },
        { "mins", "minutes" },
        { "min", "minute" },
        { "hrs", "hours" },
        { "bf", "boyfriend" },
        { "gf", "girlfriend" },
        { "bff", "best friend" },
        { "sus", "suspicious" }
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
        "careful", "just about the book", "only books", "keep it private", "mind your business",
        "keep my info private", "not comfortable sharing", "report this account", "blocking this account",
        "save screenshots", "talk to a counselor", "tell a teacher", "talk to my mom", "talk to my dad",
        "keep school offline", "take my picture down", "delete my photo", "that crosses a line",
        "do not save my picture"
    };

    private static readonly string[] RiskyGeneralPatterns =
    {
        "rainfall", "maple avenue", "maple street", "cedar park", "my address",
        "where i live", "live near", "walk home", "walking home", "every day",
        "home alone", "parents are away", "parents are busy", "parents are never home", "no one checks",
        "fifteen", "15 years old", "sophomore", "send you everything", "trust you",
        "here is my password", "my password", "my login", "my email", "account details",
        "shut up", "you idiot", "stupid", "hate you", "fight me",
        "do not care what happens", "leave me alone or else", "you are pathetic", "post whatever",
        "add me on snapchat", "follow my instagram", "message me on instagram", "nobody is home",
        "send another selfie", "accepting every request", "get more followers", "handle it all alone"
    };

    private static readonly HashSet<string> NegationTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "not", "no", "never", "refuse", "stop", "cannot", "avoid", "wont", "dont", "nope", "neither", "decline", "without",
        "nah", "quit", "cease", "refusing", "declining"
    };

    private static readonly string[] SensitiveDisclosureTokens =
    {
        "school", "rainfall", "street", "avenue", "road", "park", "address", "home", "house",
        "live", "route", "walk", "gate", "bus", "age", "old", "grade", "sophomore", "fifteen", "15",
        "password", "login", "email", "account", "details", "selfie", "picture", "photo",
        "instagram", "snapchat", "location"
    };

    private static readonly Dictionary<string, string[]> ExpandedAbbreviationMap;
    private static readonly CompiledPattern[] CompiledSafeGeneralPatterns;
    private static readonly CompiledPattern[] CompiledRiskyGeneralPatterns;

    [ThreadStatic] private static int[] s_levenshteinScratch;
    [ThreadStatic] private static StringBuilder s_tokenBuilder;
    [ThreadStatic] private static List<string> s_rawTokensScratch;
    [ThreadStatic] private static Dictionary<string, CompiledPattern> s_patternCache;

    static PhoneTextClassifier()
    {
        ExpandedAbbreviationMap = new Dictionary<string, string[]>(AbbreviationMap.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in AbbreviationMap)
        {
            ExpandedAbbreviationMap[pair.Key] = pair.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        CompiledSafeGeneralPatterns = CompilePatterns(SafeGeneralPatterns);
        CompiledRiskyGeneralPatterns = CompilePatterns(RiskyGeneralPatterns);
    }

    /// <summary>
    /// Tokenizes and expands English chat input using the same pipeline used during classification.
    /// </summary>
    public static List<string> TokenizeInput(string rawText)
    {
        return TokenizeAndExpand(rawText);
    }

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

        safeScore += ScoreGeneralPatterns(inputTokens, CompiledSafeGeneralPatterns, suppressIfNegated: false);
        riskyScore += ScoreGeneralPatterns(inputTokens, CompiledRiskyGeneralPatterns, suppressIfNegated: true);

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
    /// Computes the standard Levenshtein edit distance between two lowercase strings using stackalloc/pooled row buffers.
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

        if (string.Equals(source, target, StringComparison.Ordinal))
        {
            return 0;
        }

        // Ensure target is the shorter string so the row buffer size (m + 1) is minimized.
        if (source.Length < target.Length)
        {
            string swap = source;
            source = target;
            target = swap;
        }

        int n = source.Length;
        int m = target.Length;
        int rowLength = m + 1;
        int totalInts = rowLength * 2;

        Span<int> buffer = totalInts <= StackAllocIntThreshold
            ? stackalloc int[totalInts]
            : GetScratchBuffer(totalInts);

        Span<int> previousRow = buffer.Slice(0, rowLength);
        Span<int> currentRow = buffer.Slice(rowLength, rowLength);

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
                int minCost = insertCost < deleteCost ? insertCost : deleteCost;
                currentRow[j] = replaceCost < minCost ? replaceCost : minCost;
            }

            Span<int> temp = previousRow;
            previousRow = currentRow;
            currentRow = temp;
        }

        return previousRow[m];
    }

    private static bool WithinLevenshteinDistance(string source, string target, int maxDistance)
    {
        if (source.Length < target.Length)
        {
            string swap = source;
            source = target;
            target = swap;
        }

        int n = source.Length;
        int m = target.Length;
        int rowLength = m + 1;
        int totalInts = rowLength * 2;

        Span<int> buffer = totalInts <= StackAllocIntThreshold
            ? stackalloc int[totalInts]
            : GetScratchBuffer(totalInts);

        Span<int> previousRow = buffer.Slice(0, rowLength);
        Span<int> currentRow = buffer.Slice(rowLength, rowLength);

        for (int j = 0; j <= m; j++)
        {
            previousRow[j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            currentRow[0] = i;
            int rowMin = i;
            char sourceChar = source[i - 1];

            for (int j = 1; j <= m; j++)
            {
                int cost = sourceChar == target[j - 1] ? 0 : 1;
                int insertCost = currentRow[j - 1] + 1;
                int deleteCost = previousRow[j] + 1;
                int replaceCost = previousRow[j - 1] + cost;
                int minCost = insertCost < deleteCost ? insertCost : deleteCost;
                int cell = replaceCost < minCost ? replaceCost : minCost;
                currentRow[j] = cell;
                if (cell < rowMin)
                {
                    rowMin = cell;
                }
            }

            if (rowMin > maxDistance)
            {
                return false;
            }

            Span<int> temp = previousRow;
            previousRow = currentRow;
            currentRow = temp;
        }

        return previousRow[m] <= maxDistance;
    }

    private static int[] GetScratchBuffer(int requiredLength)
    {
        if (s_levenshteinScratch == null || s_levenshteinScratch.Length < requiredLength)
        {
            s_levenshteinScratch = new int[requiredLength];
        }

        return s_levenshteinScratch;
    }

    private static CompiledPattern[] CompilePatterns(string[] rawPatterns)
    {
        if (rawPatterns == null || rawPatterns.Length == 0)
        {
            return Array.Empty<CompiledPattern>();
        }

        CompiledPattern[] compiled = new CompiledPattern[rawPatterns.Length];
        for (int i = 0; i < rawPatterns.Length; i++)
        {
            compiled[i] = BuildCompiledPattern(rawPatterns[i]);
        }
        return compiled;
    }

    private static CompiledPattern GetCompiledPattern(string patternText)
    {
        if (s_patternCache == null)
        {
            s_patternCache = new Dictionary<string, CompiledPattern>(512, StringComparer.Ordinal);
        }

        if (s_patternCache.TryGetValue(patternText, out CompiledPattern cached))
        {
            return cached;
        }

        CompiledPattern compiled = BuildCompiledPattern(patternText);
        if (s_patternCache.Count >= PatternCacheLimit)
        {
            s_patternCache.Clear();
        }
        s_patternCache[patternText] = compiled;
        return compiled;
    }

    private static CompiledPattern BuildCompiledPattern(string patternText)
    {
        if (string.IsNullOrWhiteSpace(patternText))
        {
            return new CompiledPattern(Array.Empty<string>(), string.Empty, 0);
        }

        List<string> tokens = TokenizeAndExpand(patternText);
        if (tokens.Count == 0)
        {
            return new CompiledPattern(Array.Empty<string>(), string.Empty, 0);
        }

        string[] tokenArray = tokens.ToArray();
        string joined = tokenArray.Length > 1 ? string.Concat(tokenArray) : string.Empty;
        int weight = tokenArray.Length > 1 ? PhraseMatchWeight : KeywordMatchWeight;
        return new CompiledPattern(tokenArray, joined, weight);
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

            CompiledPattern pattern = GetCompiledPattern(keyword);
            if (pattern.Tokens.Length == 0)
            {
                continue;
            }

            int matchIndex = FindTokenPatternMatch(inputTokens, pattern);
            if (matchIndex >= 0 && (!suppressIfNegated || !IsTokenNegated(inputTokens, matchIndex)))
            {
                score += pattern.KeywordWeight;
            }
        }

        return score;
    }

    private static int ScoreGeneralPatterns(List<string> inputTokens, CompiledPattern[] patterns, bool suppressIfNegated)
    {
        int score = 0;
        for (int i = 0; i < patterns.Length; i++)
        {
            CompiledPattern pattern = patterns[i];
            if (pattern.Tokens == null || pattern.Tokens.Length == 0)
            {
                continue;
            }

            int matchIndex = FindTokenPatternMatch(inputTokens, pattern);
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

            if (!isSensitive)
            {
                continue;
            }

            // Avoid treating truancy phrases like "not going to school" or "won't go to school" as Safe privacy refusals.
            if (IsFuzzyTokenMatch(token, "school"))
            {
                if (i >= 2 && string.Equals(inputTokens[i - 1], "to", StringComparison.Ordinal))
                {
                    string verb = inputTokens[i - 2];
                    if (verb == "go" || verb == "going" || verb == "head" || verb == "heading" || verb == "come" || verb == "coming")
                    {
                        continue;
                    }
                }

                // Avoid treating vulnerability mentions like "never home and nobody at school cares" as privacy refusals.
                if (i >= 1 && string.Equals(inputTokens[i - 1], "at", StringComparison.Ordinal))
                {
                    continue;
                }
            }

            // Avoid treating unsupervised-home disclosures ("parents are never home", "mom is not home") as privacy refusals.
            if (IsFuzzyTokenMatch(token, "home") || IsFuzzyTokenMatch(token, "house"))
            {
                bool hasFamilyContext = false;
                int start = Math.Max(0, i - 4);
                for (int k = start; k < i; k++)
                {
                    string ctx = inputTokens[k];
                    if (ctx == "parents" || ctx == "mom" || ctx == "dad" || ctx == "family" || ctx == "nobody")
                    {
                        hasFamilyContext = true;
                        break;
                    }
                }
                if (hasFamilyContext)
                {
                    continue;
                }
            }

            if (IsTokenNegated(inputTokens, i))
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

    private static int FindTokenPatternMatch(List<string> inputTokens, in CompiledPattern pattern)
    {
        string[] patternTokens = pattern.Tokens;
        int patternCount = patternTokens.Length;
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
        string joinedPattern = pattern.JoinedCompact;
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

        return WithinLevenshteinDistance(inputToken, targetToken, maxDistance);
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
            if (ExpandedAbbreviationMap.TryGetValue(token, out string[] parts))
            {
                for (int p = 0; p < parts.Length; p++)
                {
                    expandedTokens.Add(parts[p]);
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
        if (s_rawTokensScratch == null)
        {
            s_rawTokensScratch = new List<string>(16);
        }
        else
        {
            s_rawTokensScratch.Clear();
        }

        if (string.IsNullOrEmpty(rawText))
        {
            return s_rawTokensScratch;
        }

        if (s_tokenBuilder == null)
        {
            s_tokenBuilder = new StringBuilder(32);
        }
        else
        {
            s_tokenBuilder.Clear();
        }

        for (int i = 0; i < rawText.Length; i++)
        {
            char c = rawText[i];
            if (char.IsLetterOrDigit(c))
            {
                s_tokenBuilder.Append(char.ToLowerInvariant(c));
            }
            else if (c == '\'' || c == '\u2019')
            {
                // Strip apostrophes inside contractions ("don't" -> "dont") so they match AbbreviationMap.
                continue;
            }
            else
            {
                if (s_tokenBuilder.Length > 0)
                {
                    s_rawTokensScratch.Add(s_tokenBuilder.ToString());
                    s_tokenBuilder.Clear();
                }
            }
        }

        if (s_tokenBuilder.Length > 0)
        {
            s_rawTokensScratch.Add(s_tokenBuilder.ToString());
            s_tokenBuilder.Clear();
        }

        return s_rawTokensScratch;
    }
}
