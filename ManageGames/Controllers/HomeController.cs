using System.Diagnostics;
using ManageGames.Auth;
using ManageGames.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManageGames.Controllers
{
    /// <summary>The public pages. Everything else requires a signed-in user.</summary>
    [AllowAnonymous]
    public class HomeController : Controller
    {
        // The start page hosts the login form; signed-in users go straight to their games.
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Games");
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowPendingPasswordChange]
        [IgnoreAntiforgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
