using Microsoft.AspNetCore.Identity;

namespace ManageGames.Auth;

/// <summary>
/// Identity looks users up by a normalized name. Lower-casing and removing spaces keeps the app's
/// long-standing rule that "Max", "max" and "m ax" are the same account (and matches the names the
/// migration copied from the old user table).
/// </summary>
public sealed class UsernameNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name)
    {
        return name?.Trim().ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    public string? NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant();
    }
}
