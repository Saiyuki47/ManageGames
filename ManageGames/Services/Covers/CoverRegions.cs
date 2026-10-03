namespace ManageGames.Services.Covers;

/// <summary>The preferred box art regions (<see cref="CoverOptions.Regions"/>).</summary>
public static class CoverRegions
{
    /// <summary>"de, EU" → de, eu.</summary>
    public static IReadOnlyList<string> Parse(string regions)
    {
        return regions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => r.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>0 for the most preferred region, then 1, ...; regions that aren't preferred (or unknown) come last.</summary>
    public static int Rank(IReadOnlyList<string> preferred, string? region)
    {
        for (var i = 0; i < preferred.Count; i++)
        {
            if (string.Equals(preferred[i], region, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return preferred.Count;
    }
}
