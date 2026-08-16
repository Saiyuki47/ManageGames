using ManageGames.Models;
using ManageGames.Service;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ManageGames.ViewModels;
using ManageGames.Filters;
using ManageGames.Helpers;

namespace ManageGames.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class HomeController : Controller
    {
        private readonly DataBase_Service _service;
        public HomeController(DataBase_Service service)
        {
            _service = service;
        }

        // The authenticated user's id, guaranteed present inside [RequireLogin] actions
        // (RequireLoginAttribute validates the session cookie and stashes it here).
        private Guid CurrentUserId => (Guid)HttpContext.Items[SessionCookie.UserIdItemKey]!;

        // Robustly reads and validates the session cookie for the public Index page.
        // Returns null when the cookie is absent, malformed, or no longer matches a user.
        private Guid? GetValidatedUserId()
        {
            HttpContext.Request.Cookies.TryGetValue(SessionCookie.CookieName, out var cookie);
            if (SessionCookie.TryParse(cookie, out var userId, out var cookieId)
                && _service.IsValidSession(userId, cookieId))
            {
                return userId;
            }
            return null;
        }

        public IActionResult Index(string searchString, bool logInFailed = false)
        {

            if (logInFailed)
            {
                ViewBag.FalscheAnmeldung = "Failed";
            }
            // Anonymous visitors see nothing (no cross-user data leak); a logged-in user sees
            // only their own games.
            IndexModel indexModel = new IndexModel() { GamesList = new List<GameModel>() };

            if (HttpContext.Request.Cookies.ContainsKey(SessionCookie.CookieName))
            {
                Guid? userId = GetValidatedUserId();
                if (userId == null)
                {
                    // Stale or malformed cookie -> clear it and reload.
                    HttpContext.Response.Cookies.Delete(SessionCookie.CookieName);
                    return RedirectToAction("Index");
                }
                indexModel.GamesList = _service.GetGamesForUser(userId.Value, onWishList: false);
            }

            indexModel.GamesList = SearchFilter.Filter(indexModel.GamesList, searchString, x => x.GameName);
            ViewBag.IsStartseite = "Yes";
            return View("Index", indexModel);
        }
        [RequireLogin]
        public IActionResult WishList(string searchString)
        {
            WishListModel wishlistmodel = new WishListModel() { WishList = _service.GetGamesForUser(CurrentUserId, onWishList: true) };
            wishlistmodel.WishList = SearchFilter.Filter(wishlistmodel.WishList, searchString, x => x.GameName);

            return View("WishList", wishlistmodel);
        }

        #region Game

        [HttpPost]
        [RequireLogin]
        public IActionResult DeleteGame(int id)
        {
            _service.DeleteGame(id, CurrentUserId);
            return RedirectToAction("Index");
        }
        [RequireLogin]
        public IActionResult AddGame()
        {

            AddGamesModel addGamesModel = new AddGamesModel();
            addGamesModel.ConsoleList = _service.GetCategoryList();/*.OrderBy(x => x.ConsoleName).ToList();*/

            return View("AddGame", addGamesModel);
        }
        [HttpPost]
        [RequireLogin]
        public IActionResult AddGame(string gameName, int game_amount, int? consoles, string wishlist)
        {
            // Owner comes from the authenticated session, never from request input.
            _service.AddGame(gameName, game_amount, consoles, wishlist, CurrentUserId);
            return RedirectToAction("Index");

        }
        [HttpPost]
        [RequireLogin]
        public IActionResult EditGame(int id)
        {
            EditGameModel editgamemodel = new EditGameModel();
            editgamemodel.ConsoleList = _service.GetCategoryList();
            editgamemodel.Game = _service.GetSingleGame(id, CurrentUserId);

            return View("EditGame", editgamemodel);
        }
        [HttpPost]
        [RequireLogin]
        public IActionResult SaveEditedGame(string gameName, int game_amount, int? consoles, string wishlist, int id)
        {
            _service.UpdateGame(gameName, game_amount, consoles, wishlist, id, CurrentUserId);
            return RedirectToAction("Index");
        }

        #endregion

        #region Category

        [RequireAdmin]
        public IActionResult EditCategory(int id)
        {

            return View("EditCategory", new AddEditCategory()
            {
                Console = _service.GetSingleConsole(id),
                CompanyList = _service.GetCompanyList()
            });
        }
        [HttpPost]
        [RequireAdmin]
        public IActionResult SaveEditedCategory(int category_id, string categoryName, int? company)
        {
            _service.UpdateCategory(category_id, categoryName, company);
            return RedirectToAction("CategoryList");
        }
        [RequireAdmin]
        public IActionResult CategoryList(string searchString)
        {
            CategoryListModel categorylist = new CategoryListModel()
            {
                ConsoleList = _service.GetCategoryList()
            };

            categorylist.ConsoleList = SearchFilter.Filter(categorylist.ConsoleList, searchString, x => x.ConsoleName);

            return View("CategoryList", categorylist);
        }
        [HttpPost]
        [RequireAdmin]
        public IActionResult DeleteCategory(int id)
        {
            _service.DeleteCategory(id);
            return RedirectToAction("CategoryList");
        }

        [RequireAdmin]
        public IActionResult AddCategory()
        {

            return View(new AddEditCategory() { CompanyList = _service.GetCompanyList() });
        }
        [HttpPost]
        [RequireAdmin]
        public IActionResult AddCategory(string categoryName, int? company)
        {
            _service.AddCategory(categoryName, company);
            return RedirectToAction("CategoryList");
        }

        #endregion

        #region Company

        [RequireAdmin]
        public IActionResult CompanyList(string searchString)
        {
            CompanyListModel companylist = new CompanyListModel()
            {
                CompanyList = _service.GetCompanyList()
            };

            companylist.CompanyList = SearchFilter.Filter(companylist.CompanyList, searchString, x => x.CompanyName);

            return View("CompanyList", companylist);
        }
        [RequireAdmin]
        public IActionResult AddCompany()
        {

            return View();
        }
        [HttpPost]
        [RequireAdmin]
        public IActionResult AddCompany(string companyName)
        {
            _service.AddCompany(companyName);
            return RedirectToAction("CompanyList");
        }
        [RequireAdmin]
        public IActionResult EditCompany(int id)
        {

            return View("EditCompany", new AddEditCompany() { Company = _service.GetSingleCompany(id) });
        }
        [HttpPost]
        [RequireAdmin]
        public IActionResult SaveEditedCompany(int company_id, string companyName)
        {
            _service.UpdateCompany(company_id, companyName);
            return RedirectToAction("CompanyList");
        }
        [HttpPost]
        [RequireAdmin]
        public IActionResult DeleteCompany(int id)
        {
            _service.DeleteCompany(id);
            return RedirectToAction("CompanyList");
        }

        #endregion

        [RequireAdmin]
        public IActionResult AddUser()
        {
            return View();
        }

        [HttpPost]
        [RequireAdmin]
        public IActionResult AddUser(string username, string password, bool isAdmin)
        {
            // Required fields are also enforced client-side; re-check here for crafted requests.
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            // Fails only on a duplicate username now that the fields are known non-empty.
            if (!_service.CreateUser(username, password, isAdmin))
            {
                ViewBag.Error = "This username is already taken.";
                return View();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult LoginValidation(string username, string password)
        {
            string cookieValue = LoginCheck(username, password);
            //Wenn der Login fehlschlägt dan miese kriese
            if (string.IsNullOrEmpty(cookieValue))
            {
                return Index("", true);
            }
            else
            {
                CookieOptions cookieOptions = new CookieOptions();
                //Cookie Ablaufdatum/Uhrzeit festlegen
                cookieOptions.Expires = new DateTimeOffset(DateTime.Now.AddSeconds(600));
                if (HttpContext.Request.Cookies.ContainsKey(SessionCookie.CookieName))
                {
                    //Bestehenden cookie mit den Nutzerdaten/ Cookiedaten vergleichen
                    HttpContext.Response.Cookies.Delete(SessionCookie.CookieName);

                }
                HttpContext.Response.Cookies.Append(SessionCookie.CookieName, cookieValue, cookieOptions);
            }
            return RedirectToAction("Index");

        }

        private string LoginCheck(string username, string password)
        {
            UserModel? user = _service.ValidateCredentials(username, password);
            if (user == null)
            {
                return string.Empty;
            }

            string cookieID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);
            _service.ChangeCookieId(user.UserID, cookieID);
            return user.UserID.ToString() + "+" + cookieID;
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}