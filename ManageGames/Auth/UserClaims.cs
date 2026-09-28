using System.Security.Claims;
using ManageGames.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ManageGames.Auth
{
    /// <summary>
    /// Builds the claims stored in the auth cookie at sign-in and reads them back.
    /// </summary>
    public static class UserClaims
    {
        public const string SecurityStampType = "ManageGames.SecurityStamp";
        public const string MustChangePasswordType = "ManageGames.MustChangePassword";

        public static ClaimsPrincipal CreatePrincipal(UserModel user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(SecurityStampType, user.SecurityStamp),
            };
            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, Roles.Admin));
            }
            if (user.MustChangePassword)
            {
                claims.Add(new Claim(MustChangePasswordType, "true"));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        /// <summary>The signed-in user's id. Only call it for authenticated principals.</summary>
        public static Guid GetUserId(this ClaimsPrincipal principal)
        {
            return Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        public static bool HasPendingPasswordChange(this ClaimsPrincipal principal)
        {
            return principal.HasClaim(c => c.Type == MustChangePasswordType);
        }
    }
}
