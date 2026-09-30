using ManageGames.Auth;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ManageGames.Controllers
{
    /// <summary>Login, logout and the signed-in user's own password.</summary>
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserService _users;
        private readonly IAntiforgery _antiforgery;

        public AccountController(UserService users, IAntiforgery antiforgery)
        {
            _users = users;
            _antiforgery = antiforgery;
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        // Validated below: the form's token belongs to an anonymous visitor, so the global check would
        // reject it with 400 when the request already carries a valid auth cookie.
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Login(LoginViewModel form)
        {
            // Already signed in, e.g. in another tab, or a cross-site link showed the login form because
            // the SameSite=Strict cookie wasn't sent with it: keep the existing session.
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectAfterLogin(form.ReturnUrl);
            }
            if (!await _antiforgery.IsRequestValidAsync(HttpContext))
            {
                return BadRequest();
            }

            var user = _users.ValidateCredentials(form.Username, form.Password);
            if (user == null)
            {
                // Redirect instead of rendering the page, so reloading it doesn't resend the password.
                TempData[TempDataKeys.LoginError] = "Wrong username or password.";
                return RedirectToAction("Index", "Home", new { form.ReturnUrl });
            }

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserClaims.CreatePrincipal(user));
            return RedirectAfterLogin(form.ReturnUrl);
        }

        [HttpPost]
        [AllowAnonymous]
        [AllowPendingPasswordChange]
        // The session may already have ended (expired, or logged out on another device), which leaves the
        // page's token bound to a user the request no longer has. Logging out is harmless, and cross-site
        // posts can't log anyone out since they don't carry the SameSite=Strict cookie.
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                // Revoke the session server-side too: a copy of the cookie must not stay usable.
                _users.EndSessions(User.GetUserId());
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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

            var userId = User.GetUserId();
            if (!_users.ChangePassword(userId, form.CurrentPassword, form.NewPassword))
            {
                ModelState.AddModelError(nameof(form.CurrentPassword), "The current password is wrong.");
                return View(form);
            }

            // The change rotated the security stamp, which ended all other sessions of this user.
            // Re-issue the cookie so this session carries the new stamp and continues.
            var user = _users.GetUser(userId)!;
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserClaims.CreatePrincipal(user));

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
}
