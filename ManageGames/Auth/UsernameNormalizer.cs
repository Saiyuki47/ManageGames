using Microsoft.AspNetCore.Identity;

namespace ManageGames.Auth;

/// <summary>
/// Identity looks users up by a normalized name. Lower-casing and removing spaces makes "Max", "max"
/// and "m ax" the same account, so nobody can register a look-alike of an existing name.
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
