using System.Security.Claims;
using ManageGames.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace ManageGames.Auth;

/// <summary>
/// Ties the auth cookie to a server-side session (see <see cref="SessionService"/>): signing in starts one,
/// every request checks it, and signing out ends it. So logging out revokes exactly this browser's
/// cookie, including any copy of it, while the user's other devices stay signed in.
/// </summary>
public class AuthCookieEvents(SessionService sessions) : CookieAuthenticationEvents
{
    public override async Task SigningIn(CookieSigningInContext context)
    {
        if (context.Principal?.Identity is ClaimsIdentity identity
            && Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            // A new sign-in (also after a password change) always gets a new session id.
            foreach (var claim in identity.FindAll(UserClaims.SessionIdType).ToList())
            {
                identity.RemoveClaim(claim);
            }
            var sessionId = await sessions.StartAsync(userId, context.HttpContext.RequestAborted);
            identity.AddClaim(new Claim(UserClaims.SessionIdType, sessionId));
        }
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var sessionId = principal?.GetSessionId();
        if (principal == null
            || sessionId == null
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !await sessions.IsActiveAsync(sessionId, userId, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        // Identity's own check: the security stamp, which password changes and resets rotate, and
        // refreshed claims. Setting EventsType replaces Identity's events, so it has to be called here.
        await SecurityStampValidator.ValidatePrincipalAsync(context);
    }

    public override async Task SigningOut(CookieSigningOutContext context)
    {
        var sessionId = context.HttpContext.User.GetSessionId();
        if (sessionId != null)
        {
            // Not cancelled with the request: the session has to end even if the browser stops waiting.
            await sessions.EndAsync(sessionId, CancellationToken.None);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        // A form post whose session has ended can't be repeated: returning to its URL after the
        // login would GET a POST-only action (405). Return to the start page instead.
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.RedirectUri = context.Request.PathBase + context.Options.LoginPath
                + QueryString.Create(context.Options.ReturnUrlParameter, context.Request.PathBase + "/");
        }
        return base.RedirectToLogin(context);
    }
}
