using System.Security.Claims;

namespace ManageGames.Auth;

/// <summary>The app's own claims in the auth cookie, next to the ones ASP.NET Core Identity adds.</summary>
public static class UserClaims
{
    public const string MustChangePasswordType = "ManageGames.MustChangePassword";
    public const string SessionIdType = "ManageGames.SessionId";

    /// <summary>The signed-in user's id. Only call it for authenticated principals.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    public static string? GetSessionId(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(SessionIdType);
    }

    public static bool HasPendingPasswordChange(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(c => c.Type == MustChangePasswordType);
    }
}
