using System.Globalization;
using ManageGames.Auth;
using ManageGames.Helpers;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.Controllers;

/// <summary>The signed-in user's own game collection and wishlist.</summary>
[Authorize]
public class GamesController(GameService games, ConsoleService consoles) : Controller
{
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

        games.AddGame(User.GetUserId(), form.Name, form.Copies, form.ConsoleId, form.OnWishList);
        return RedirectToList(form.OnWishList);
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var game = games.GetGame(id, User.GetUserId());
        if (game == null)
        {
            return NotFound();
        }

        return EditView(new GameFormViewModel
        {
            Id = game.Id,
            Name = game.Name,
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

        if (!games.UpdateGame(id, User.GetUserId(), form.Name, form.Copies, form.ConsoleId, form.OnWishList))
        {
            return NotFound();
        }
        return RedirectToList(form.OnWishList);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        var game = games.DeleteGame(id, User.GetUserId());
        if (game == null)
        {
            return NotFound();
        }
        return RedirectToList(game.IsOnWishList);
    }

    private ViewResult List(bool onWishList, string? search)
    {
        var ownGames = games.GetGames(User.GetUserId(), onWishList);
        return View("Index", new GameListViewModel
        {
            Games = SearchFilter.Filter(ownGames, search, g => g.Name),
            Search = search,
            IsWishList = onWishList,
        });
    }

    private ViewResult EditView(GameFormViewModel form)
    {
        form.ConsoleOptions = consoles.GetConsoles()
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        return View("Edit", form);
    }

    private RedirectToActionResult RedirectToList(bool onWishList)
    {
        return RedirectToAction(onWishList ? nameof(Wishlist) : nameof(Index));
    }
}
