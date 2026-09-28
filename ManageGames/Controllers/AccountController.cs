using ManageGames.Auth;
using ManageGames.Services;
using ManageGames.ViewModels;
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

        public AccountController(UserService users)
        {
            _users = users;
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<IActionResult> Login(LoginViewModel form)
        {
            var user = _users.ValidateCredentials(form.Username, form.Password);
            if (user == null)
            {
                // Redirect instead of rendering the page, so reloading it doesn't resend the password.
                TempData[TempDataKeys.LoginError] = "Wrong username or password.";
                return RedirectToAction("Index", "Home", new { form.ReturnUrl });
            }

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserClaims.CreatePrincipal(user));

            // Only follow local return URLs, so the login can't be abused as an open redirect.
            if (Url.IsLocalUrl(form.ReturnUrl))
            {
                return LocalRedirect(form.ReturnUrl);
            }
            return RedirectToAction("Index", "Games");
        }

        [HttpPost]
        [AllowPendingPasswordChange]
        public async Task<IActionResult> Logout()
        {
            // Revoke the session server-side too: a copy of the cookie must not stay usable.
            _users.EndSessions(User.GetUserId());
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
    }
}
