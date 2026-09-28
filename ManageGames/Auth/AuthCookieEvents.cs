using System.Security.Claims;
using ManageGames.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ManageGames.Auth
{
    /// <summary>
    /// Server-side check of every auth cookie: it is only honored while its user still exists and
    /// the security stamp inside it matches the database. Logging out and changing or resetting a
    /// password rotate the stamp, which revokes every session issued before. Deleting the cookie in
    /// the browser alone is not enough, since a copy of it would stay valid until it expires.
    /// </summary>
    public class AuthCookieEvents : CookieAuthenticationEvents
    {
        private readonly UserService _users;

        public AuthCookieEvents(UserService users)
        {
            _users = users;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            if (principal != null
                && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                && _users.GetSecurityStamp(userId) is { } stamp
                && stamp == principal.FindFirstValue(UserClaims.SecurityStampType))
            {
                return;
            }

            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
