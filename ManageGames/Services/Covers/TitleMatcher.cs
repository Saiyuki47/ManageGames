using System.Globalization;
using System.Text;

namespace ManageGames.Services.Covers;

/// <summary>
/// Decides how well a search result fits a game, so the automatic search only takes covers it is sure about:
/// a wrong cover is worse than none, which the user can still pick by hand.
/// </summary>
public static class TitleMatcher
{
    /// <summary>The automatic search only takes results at least this similar to the game's title.</summary>
    public const double AcceptThreshold = 0.8;

    // Leading maker names that are often left out: "Switch" is the "Nintendo Switch", "Dreamcast" the "Sega Dreamcast".
    private static readonly HashSet<string> Makers = new(StringComparer.Ordinal)
    {
        "analogue", "atari", "bandai", "coleco", "commodore", "magnavox", "mattel", "microsoft", "nec",
        "nintendo", "nokia", "panasonic", "panic", "philips", "sega", "snk", "sony", "tiger", "valve",
    };

    private static readonly Dictionary<string, string> RomanNumerals = new(StringComparer.Ordinal)
    {
        ["ii"] = "2",
        ["iii"] = "3",
        ["iv"] = "4",
        ["vi"] = "6",
        ["vii"] = "7",
        ["viii"] = "8",
        ["ix"] = "9",
        ["xi"] = "11",
        ["xii"] = "12",
        ["xiii"] = "13",
        ["xiv"] = "14",
        ["xv"] = "15",
        ["xvi"] = "16",
    };

    /// <summary>
    /// Similarity of two titles from 0 (unrelated) to 1 (the same after normalization), ignoring case, accents,
    /// punctuation and the word "Version". Titles with different numbers never match: "Mario Kart 7" is not
    /// "Mario Kart 8", "Splatoon" is not "Splatoon 2" and "Pikmin" is not "Pikmin²". A shortened series name
    /// is fine when the subtitles match: "Zelda: Link's Awakening" is "The Legend of Zelda: Link's Awakening".
    /// </summary>
    public static double Similarity(string title, string other)
    {
        var a = TitleTokens(title);
        var b = TitleTokens(other);
        if (a.Count == 0 || b.Count == 0 || !Numbers(a).SequenceEqual(Numbers(b), StringComparer.Ordinal))
        {
            return 0;
        }

        var similarity = WordSimilarity(a, b);
        if (SplitSeries(title) is var (seriesA, subtitleA) && SplitSeries(other) is var (seriesB, subtitleB)
            && IsShortFormOf(TitleTokens(seriesA), TitleTokens(seriesB)))
        {
            var subA = TitleTokens(subtitleA);
            var subB = TitleTokens(subtitleB);
            if (subA.Count > 0 && subB.Count > 0)
            {
                similarity = Math.Max(similarity, WordSimilarity(subA, subB));
            }
        }
        return similarity;
    }

    /// <summary>The similarity of the game's title to the result's title or to its best-fitting alternative title.</summary>
    public static double Similarity(string title, CoverCandidate candidate)
    {
        return candidate.AlternativeTitles.Prepend(candidate.Title).Max(t => Similarity(title, t));
    }

    // Shared words cope with extra or missing words ("Zelda: Breath of the Wild"), the compact comparison
    // with differently split words and small typos ("Pokémon LeafGreen" vs. "Pokemon Leaf Green").
    private static double WordSimilarity(List<string> a, List<string> b)
    {
        var shared = a.Intersect(b, StringComparer.Ordinal).Count();
        var wordSimilarity = (double)shared / a.Union(b, StringComparer.Ordinal).Count();
        var compactA = string.Concat(a);
        var compactB = string.Concat(b);
        var compactSimilarity = 1 - (double)Levenshtein(compactA, compactB) / Math.Max(compactA.Length, compactB.Length);
        return Math.Max(wordSimilarity, compactSimilarity);
    }

    // "Series: Subtitle" or "Series – Subtitle"; null without such a split.
    private static (string Series, string Subtitle)? SplitSeries(string title)
    {
        var colon = title.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            return (title[..colon], title[(colon + 1)..]);
        }
        var dash = title.IndexOfAny(['–', '—']);
        return dash > 0 ? (title[..dash], title[(dash + 1)..]) : null;
    }

    // "Zelda" is short for "The Legend of Zelda": all words of one appear in the other.
    private static bool IsShortFormOf(List<string> a, List<string> b)
    {
        return a.Count > 0 && b.Count > 0 && (a.TrueForAll(b.Contains) || b.TrueForAll(a.Contains));
    }

    /// <summary>
    /// Whether the result is for the game's console: true or false, or null when the game has no console or the
    /// source doesn't list platforms. Maker names may be left out ("Switch" = "Nintendo Switch"), but the
    /// rest must be equal ("Game Boy" is not "Game Boy Advance").
    /// </summary>
    public static bool? PlatformMatches(string? console, CoverCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(console))
        {
            return null;
        }

        // Some sources list regional names together: "Sega Mega Drive/Genesis".
        var names = candidate.Platforms.Concat(candidate.PlatformAliases)
            .SelectMany(p => p.Split(['/', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        if (names.Count == 0)
        {
            return null;
        }

        var wanted = PlatformKey(console);
        return names.Any(n => string.Equals(PlatformKey(n), wanted, StringComparison.Ordinal));
    }

    private static string PlatformKey(string name)
    {
        var tokens = Tokens(name);
        return string.Concat(tokens.Count > 1 && Makers.Contains(tokens[0]) ? tokens.Skip(1) : tokens);
    }

    private static List<string> TitleTokens(string title)
    {
        return Tokens(title)
            .Where(t => t != "version")
            .Select(t => RomanNumerals.GetValueOrDefault(t, t))
            .ToList();
    }

    private static IEnumerable<string> Numbers(List<string> tokens)
    {
        return tokens.Where(t => t.All(char.IsAsciiDigit)).Select(t => t.TrimStart('0')).Order(StringComparer.Ordinal);
    }

    /// <summary>
    /// Lower-case words without accents and punctuation, with numbers as words of their own:
    /// "Pokémon: Let's Go" → pokemon, lets, go; "Pikmin²" → pikmin, 2; "Wii Fit +" → wii, fit, plus.
    /// </summary>
    public static IReadOnlyList<string> Tokens(string text)
    {
        var builder = new StringBuilder(text.Length);
        // FormKD also turns "²" into "2" and other look-alike characters into their plain form.
        foreach (var c in text.Normalize(NormalizationForm.FormKD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c is '\'' or '’')
            {
                continue;
            }

            if (c == 'ß')
            {
                builder.Append("ss");
            }
            else if (c == '&')
            {
                builder.Append(" and ");
            }
            else if (c == '+')
            {
                builder.Append(" plus ");
            }
            else if (!char.IsLetterOrDigit(c))
            {
                builder.Append(' ');
            }
            else
            {
                // A number starts or ends a word: "NS2" → ns, 2, so it takes part in the number check.
                if (builder.Length > 0 && builder[^1] != ' ' && char.IsAsciiDigit(c) != char.IsAsciiDigit(builder[^1]))
                {
                    builder.Append(' ');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
        }
        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
