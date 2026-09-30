using ManageGames.Auth;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ManageGames.Controllers;

/// <summary>User administration: create accounts, reset forgotten passwords, delete accounts.</summary>
[Authorize(Roles = Roles.Admin)]
public class UsersController(UserService users, ILogger<UsersController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(new UserListViewModel { Users = await users.GetUsersAsync(), CurrentUserId = User.GetUserId() });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        // The admin only knows the initial password, so the new user has to replace it at the first login.
        var result = await users.CreateUserAsync(form.Username, form.Password, form.IsAdmin, mustChangePassword: true);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                if (error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                {
                    ModelState.AddModelError(nameof(form.Username), "This username is already taken.");
                }
                else
                {
                    ModelState.AddModelError(IsUsernameError(error) ? nameof(form.Username) : nameof(form.Password), error.Description);
                }
            }
            return View(form);
        }

        var username = form.Username.Trim();
        logger.UserCreated(User.Identity?.Name, username, form.IsAdmin);
        TempData[TempDataKeys.Message] = $"User '{username}' was created. They choose their own password at the first login.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        // Admins change their own password like everybody else, with the current one.
        if (id == User.GetUserId())
        {
            return RedirectToAction("ChangePassword", "Account");
        }

        var user = await users.GetUserAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        return View(new ResetPasswordViewModel { Username = user.UserName! });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordViewModel form)
    {
        if (id == User.GetUserId())
        {
            return RedirectToAction("ChangePassword", "Account");
        }

        var user = await users.GetUserAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        form.Username = user.UserName!;
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await users.ResetPasswordAsync(user, form.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(form.NewPassword), error.Description);
            }
            return View(form);
        }

        logger.PasswordReset(User.Identity?.Name, user.UserName!);
        TempData[TempDataKeys.Message] = $"The password of '{user.UserName}' was reset. They choose a new one at their next login.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (result, username) = await users.DeleteUserAsync(id, User.GetUserId());
        switch (result)
        {
            case DeleteUserResult.NotFound:
                return NotFound();
            case DeleteUserResult.CannotDeleteSelf:
                TempData[TempDataKeys.Error] = "You can't delete your own account.";
                break;
            default:
                logger.UserDeleted(User.Identity?.Name, username!);
                TempData[TempDataKeys.Message] = "The user and their games were deleted.";
                break;
        }
        return RedirectToAction(nameof(Index));
    }

    private static bool IsUsernameError(IdentityError error)
    {
        return error.Code is nameof(IdentityErrorDescriber.InvalidUserName) or nameof(IdentityErrorDescriber.DuplicateUserName);
    }
}
