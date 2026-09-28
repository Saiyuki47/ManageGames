using ManageGames.Auth;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManageGames.Controllers
{
    /// <summary>User administration: create accounts, reset forgotten passwords, delete accounts.</summary>
    [Authorize(Roles = Roles.Admin)]
    public class UsersController : Controller
    {
        private readonly UserService _users;

        public UsersController(UserService users)
        {
            _users = users;
        }

        public IActionResult Index()
        {
            return View(new UserListViewModel { Users = _users.GetUsers(), CurrentUserId = User.GetUserId() });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        public IActionResult Create(CreateUserViewModel form)
        {
            if (!ModelState.IsValid)
            {
                return View(form);
            }

            // The admin only knows the initial password, so the new user has to replace it at the first login.
            if (_users.CreateUser(form.Username, form.Password, form.IsAdmin, mustChangePassword: true) == CreateUserResult.UsernameTaken)
            {
                ModelState.AddModelError(nameof(form.Username), "This username is already taken.");
                return View(form);
            }

            TempData[TempDataKeys.Message] = $"User '{form.Username.Trim()}' was created. They choose their own password at the first login.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult ResetPassword(Guid id)
        {
            // Admins change their own password like everybody else, with the current one.
            if (id == User.GetUserId())
            {
                return RedirectToAction("ChangePassword", "Account");
            }

            var user = _users.GetUser(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(new ResetPasswordViewModel { Username = user.Username });
        }

        [HttpPost]
        public IActionResult ResetPassword(Guid id, ResetPasswordViewModel form)
        {
            if (id == User.GetUserId())
            {
                return RedirectToAction("ChangePassword", "Account");
            }

            var user = _users.GetUser(id);
            if (user == null)
            {
                return NotFound();
            }
            if (!ModelState.IsValid)
            {
                form.Username = user.Username;
                return View(form);
            }

            _users.ResetPassword(id, form.NewPassword);
            TempData[TempDataKeys.Message] = $"The password of '{user.Username}' was reset. They choose a new one at their next login.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Delete(Guid id)
        {
            switch (_users.DeleteUser(id, User.GetUserId()))
            {
                case DeleteUserResult.NotFound:
                    return NotFound();
                case DeleteUserResult.CannotDeleteSelf:
                    TempData[TempDataKeys.Error] = "You can't delete your own account.";
                    break;
                default:
                    TempData[TempDataKeys.Message] = "The user and their games were deleted.";
                    break;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
