using System.Diagnostics.CodeAnalysis;
using ManageGames.Auth;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ManageGames.Controllers;

/// <summary>Login, logout and the signed-in user's own password.</summary>
[Authorize]
public class AccountController(AccountService accounts, IAntiforgery antiforgery, ILogger<AccountController> logger) : Controller
{
    // One message for every failure, so it doesn't reveal whether the username exists or is locked.
    public const string LoginFailedMessage = "Wrong username or password. After several failed attempts, an account is locked for a few minutes.";

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    // Validated below: the form's token belongs to an anonymous visitor, so the global check would
    // reject it with 400 when the request already carries a valid auth cookie.
    [IgnoreAntiforgeryToken]
    [SuppressMessage("Security", "S4502", Justification = "The antiforgery token is validated in the action.")]
    public async Task<IActionResult> Login(LoginViewModel form)
    {
        // Already signed in, e.g. in another tab, or a cross-site link showed the login form because
        // the SameSite=Strict cookie wasn't sent with it: keep the existing session.
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectAfterLogin(form.ReturnUrl);
        }
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest();
        }

        var username = form.Username ?? string.Empty;
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        switch (await accounts.PasswordSignInAsync(form.Username, form.Password))
        {
            case LoginResult.Succeeded:
                logger.LoginSucceeded(username, remoteIp);
                return RedirectAfterLogin(form.ReturnUrl);
            case LoginResult.LockedOut:
                logger.LoginLockedOut(username, remoteIp);
                break;
            default:
                logger.LoginFailed(username, remoteIp);
                break;
        }

        // Redirect instead of rendering the page, so reloading it doesn't resend the password.
        TempData[TempDataKeys.LoginError] = LoginFailedMessage;
        return RedirectToAction("Index", "Home", new { form.ReturnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [AllowPendingPasswordChange]
    // The session may already have ended (expired, or ended by a password change), which leaves the
    // page's token bound to a user the request no longer has. Logging out is harmless, and cross-site
    // posts can't log anyone out since they don't carry the SameSite=Strict cookie.
    [IgnoreAntiforgeryToken]
    [SuppressMessage("Security", "S4502", Justification = "Logging out is harmless; see above.")]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Ends this browser's session on the server, so a copy of its cookie is worthless too.
            // The user's other devices stay signed in.
            await accounts.SignOutAsync();
            logger.LoggedOut(User.Identity.Name);
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowPendingPasswordChange]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [AllowPendingPasswordChange]
    [EnableRateLimiting(RateLimitPolicies.PasswordChange)]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel form)
    {
        if (ModelState.IsValid && form.NewPassword == form.CurrentPassword)
        {
            ModelState.AddModelError(nameof(form.NewPassword), "Choose a password that differs from the current one.");
        }
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        // On success this ends all other sessions of the user and continues this one with a new session.
        var result = await accounts.ChangePasswordAsync(User, form.CurrentPassword, form.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                if (error.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                {
                    ModelState.AddModelError(nameof(form.CurrentPassword), "The current password is wrong.");
                    logger.PasswordChangeRejected(User.Identity?.Name);
                }
                else
                {
                    ModelState.AddModelError(nameof(form.NewPassword), error.Description);
                }
            }
            return View(form);
        }

        logger.PasswordChanged(User.Identity?.Name);
        TempData[TempDataKeys.Message] = "Your password was changed.";
        return RedirectToAction("Index", "Games");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectAfterLogin(string? returnUrl)
    {
        // Only follow local return URLs, so the login can't be abused as an open redirect.
        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction("Index", "Games");
    }
}
