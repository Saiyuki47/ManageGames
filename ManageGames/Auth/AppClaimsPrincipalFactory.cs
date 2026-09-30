using System.Security.Claims;
using ManageGames.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ManageGames.Auth;

/// <summary>Adds the pending-password-change flag to the claims Identity puts into the auth cookie.</summary>
public class AppClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(UserClaims.MustChangePasswordType, "true"));
        }
        return identity;
    }
}
