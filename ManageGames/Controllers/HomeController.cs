using ManageGames.Models;
using ManageGames.Service;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using ManageGames.ViewModels;

namespace ManageGames.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;

        }

        public IActionResult Index(string searchString, bool logInFailed = false)
        {

            if (logInFailed)
            {
                ViewBag.FalscheAnmeldung = "Failed";
            }
            IndexModel indexModel = new IndexModel()
            {
                GamesList = new DataBase_Service().GetGameList().Where(x => x.IsOnWishList == false).ToList()
            };

            if (HttpContext.Request.Cookies.ContainsKey("GameSort+"))
            {
                //Prueft, ob die CookieID mit der in der DB uebereinstimmt
                if (CookieValidation())
                {
                    //Wenn "true" zurueckgegeben wird stimmen die ID's nicht ueberein und der Cookie wird geloescht da es einen neueren gibt
                    HttpContext.Response.Cookies.Delete("GameSort+");
                    //Nach dem loeschen --> zurueck zur statseite
                    return RedirectToAction("Index");
                }
                else
                {
                    string cookie = HttpContext.Request.Cookies.Where(x => x.Key.Equals("GameSort+")).First().Value.Split("+")[0];
                    indexModel.GamesList = indexModel.GamesList.Where(x => x.User.UserID.Equals(Guid.Parse(cookie))).ToList();
                }
            }        
                 
            if (!String.IsNullOrEmpty(searchString))
            {
                indexModel.GamesList = indexModel.GamesList.Where(x => x.GameName.ToLower().Replace(" ",string.Empty).Contains(searchString.ToLower().Replace(" ", string.Empty))).ToList();
            }
            ViewBag.IsStartseite = "Yes";
            return View("Index",indexModel);
        }
        public IActionResult WishList(string searchString)
        {
            WishListModel wishlistmodel = new WishListModel() { WishList=new DataBase_Service().GetGameList().Where(x => x.IsOnWishList == true).ToList()};
            if (!String.IsNullOrEmpty(searchString))
            {
                wishlistmodel.WishList = wishlistmodel.WishList.Where(x => x.GameName.ToLower().Replace(" ", string.Empty).Contains(searchString.ToLower().Replace(" ", string.Empty))).ToList();
            }

            return View("WishList", wishlistmodel);
        }

        #region Game

        [HttpPost]
        public IActionResult DeleteGame(int id)
        {
            new DataBase_Service().DeleteGame(id);
            return RedirectToAction("Index");
        }
        public IActionResult AddGame()
        {

            AddGamesModel addGamesModel = new AddGamesModel();
            addGamesModel.ConsoleList = new DataBase_Service().GetCategoryList();/*.OrderBy(x => x.Console_Name).ToList();*/

            return View("AddGame", addGamesModel);
        }
        [HttpPost]
        public IActionResult AddGame(string gameName, int game_amount, int consoles, string wishlist, string userID)
        {
            new DataBase_Service().AddGame(gameName, game_amount, consoles, wishlist, userID);
            return RedirectToAction("Index");

        }
        [HttpPost]
        public IActionResult EditGame(int id)
        {
            EditGameModel editgamemodel = new EditGameModel();
            editgamemodel.ConsoleList = new DataBase_Service().GetCategoryList();
            editgamemodel.Game = new DataBase_Service().GetSingleGame(id);

            return View("EditGame", editgamemodel);
        }
        [HttpPost]
        public IActionResult SaveEditedGame(string gameName, int game_amount, int consoles, string wishlist, int id)
        {
            new DataBase_Service().UpdateGame(gameName, game_amount, consoles, wishlist, id);
            return RedirectToAction("Index");
        }

        #endregion

        #region Category

        public IActionResult EditCategory(int id)
        {

            return View("EditCategory", new AddEditCategory() { Console = new DataBase_Service().GetSingleConsole(id) });
        }
        [HttpPost]
        public IActionResult SaveEditedCategory(int category_id, string categoryName)
        {
            new DataBase_Service().UpdateCategory(category_id, categoryName);
            return RedirectToAction("CategoryList");
        }
        public IActionResult CategoryList(string searchString)
        {
            CategoryListModel categorylist = new CategoryListModel()
            {
                ConsoleList = new DataBase_Service().GetCategoryList()
            };

            if (!String.IsNullOrEmpty(searchString))
            {
                categorylist.ConsoleList = categorylist.ConsoleList.Where(x => x.Console_Name.ToLower().Replace(" ", string.Empty).Contains(searchString.ToLower().Replace(" ", string.Empty))).ToList();
            }

            return View("CategoryList", categorylist);
        }
        [HttpPost]
        public IActionResult DeleteCategory(int id)
        {
            new DataBase_Service().DeleteCategory(id);
            return RedirectToAction("CategoryList");
        }
        
        public IActionResult AddCategory()
        {

            return View();
        }
        [HttpPost]
        public IActionResult AddCategory(string categoryName)
        {
            new DataBase_Service().AddCategory(categoryName);
            return RedirectToAction("CategoryList");
        }

        #endregion

        public IActionResult AddUser()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LoginValidation(string username, string password)
        {
            string cookieValue = LoginCheck(username, password);
            //Wenn der Login fehlschlägt dan miese kriese
            if (string.IsNullOrEmpty(cookieValue))
            {
                return Index("",true);
            }
            else
            {
                CookieOptions cookieOptions = new CookieOptions();
                //Cookie Ablaufdatum/Uhrzeit festlegen
                cookieOptions.Expires = new DateTimeOffset(DateTime.Now.AddSeconds(600));
                if (HttpContext.Request.Cookies.ContainsKey("GameSort+"))
                {
                    //Bestehenden cookie mit den Nutzerdaten/ Cookiedaten vergleichen
                    HttpContext.Response.Cookies.Delete("GameSort+");

                }
                HttpContext.Response.Cookies.Append("GameSort+", cookieValue, cookieOptions);
            }
            return RedirectToAction("Index");

        }

        public string LoginCheck(string username, string password)
        {
            try
            {
                List<UserModel> userlist = new DataBase_Service().GetUserList();
                UserModel user = userlist.Where(x => TrimToLower(x.Username).Equals(TrimToLower(username))).Where(o => o.Password.Equals(password)).First();
                if (user != null)
                {
                    string cookieID = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 10);
                    new DataBase_Service().ChangeCookieId(user.UserID, cookieID);
                    return user.UserID.ToString() + "+" + cookieID;
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                return string.Empty;
            }

        }
        public string[] CookieInfoList()
        {
            string cookie = HttpContext.Request.Cookies["GameSort+"];
            return cookie.Split("+");
        }
        public bool CookieValidation()
        {
            if (HttpContext.Request.Cookies.ContainsKey("GameSort+"))
            {
                List<UserModel> userList = new DataBase_Service().GetUserList(); ;
                string[] cookieUserList = CookieInfoList();
                //UserModel user = userList.Where(o => o.UserID.Equals(Guid.Parse(cookieUserList[0]))).Where(o => o.CookieID.Equals(cookieUserList[1])).First();

                if (!userList.Any(x => x.UserID.Equals(Guid.Parse(cookieUserList[0])) && x.CookieID.Equals(cookieUserList[1])))
                {
                    return true;
                }
            }
            return false;
        }
        public string TrimToLower(string word)
        {
            string tmp = word;
            tmp = tmp.ToLower();
            tmp = tmp.Replace(" ", "");
            return tmp;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}