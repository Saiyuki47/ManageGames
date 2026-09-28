using ManageGames.Auth;
using ManageGames.Helpers;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.Controllers
{
    /// <summary>The signed-in user's own game collection and wishlist.</summary>
    [Authorize]
    public class GamesController : Controller
    {
        private readonly GameService _games;
        private readonly ConsoleService _consoles;

        public GamesController(GameService games, ConsoleService consoles)
        {
            _games = games;
            _consoles = consoles;
        }

        public IActionResult Index(string? search)
        {
            return List(onWishList: false, search);
        }

        public IActionResult Wishlist(string? search)
        {
            return List(onWishList: true, search);
        }

        [HttpGet]
        public IActionResult Create(bool wishlist = false)
        {
            return EditView(new GameFormViewModel { OnWishList = wishlist });
        }

        [HttpPost]
        public IActionResult Create(GameFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                return EditView(form);
            }

            _games.AddGame(User.GetUserId(), form.Name, form.Copies, form.ConsoleId, form.OnWishList);
            return RedirectToList(form.OnWishList);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var game = _games.GetGame(id, User.GetUserId());
            if (game == null)
            {
                return NotFound();
            }

            return EditView(new GameFormViewModel
            {
                Id = game.GameId,
                Name = game.GameName,
                ConsoleId = game.ConsoleId,
                Copies = game.Copies,
                OnWishList = game.IsOnWishList,
            });
        }

        [HttpPost]
        public IActionResult Edit(int id, GameFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                return EditView(form);
            }

            if (!_games.UpdateGame(id, User.GetUserId(), form.Name, form.Copies, form.ConsoleId, form.OnWishList))
            {
                return NotFound();
            }
            return RedirectToList(form.OnWishList);
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var game = _games.DeleteGame(id, User.GetUserId());
            if (game == null)
            {
                return NotFound();
            }
            return RedirectToList(game.IsOnWishList);
        }

        private IActionResult List(bool onWishList, string? search)
        {
            var games = _games.GetGames(User.GetUserId(), onWishList);
            return View("Index", new GameListViewModel
            {
                Games = SearchFilter.Filter(games, search, g => g.GameName),
                Search = search,
                IsWishList = onWishList,
            });
        }

        private IActionResult EditView(GameFormViewModel form)
        {
            form.ConsoleOptions = _consoles.GetConsoles()
                .Select(c => new SelectListItem(c.ConsoleName, c.ConsoleId.ToString()))
                .ToList();
            return View("Edit", form);
        }

        private IActionResult RedirectToList(bool onWishList)
        {
            return RedirectToAction(onWishList ? nameof(Wishlist) : nameof(Index));
        }
    }
}
