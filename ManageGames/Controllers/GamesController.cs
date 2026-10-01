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
    public Task<ViewResult> Index(string? search, CancellationToken cancellationToken)
    {
        return ListAsync(onWishList: false, search, cancellationToken);
    }

    public Task<ViewResult> Wishlist(string? search, CancellationToken cancellationToken)
    {
        return ListAsync(onWishList: true, search, cancellationToken);
    }

    [HttpGet]
    public Task<ViewResult> Create(bool wishlist = false, CancellationToken cancellationToken = default)
    {
        return EditViewAsync(new GameFormViewModel { OnWishList = wishlist }, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Create(GameFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await EditViewAsync(form, cancellationToken);
        }

        await games.AddGameAsync(User.GetUserId(), form.Name, form.Copies, form.ConsoleId, form.OnWishList, cancellationToken);
        return RedirectToList(form.OnWishList);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var game = await games.GetGameAsync(id, User.GetUserId(), cancellationToken);
        if (game == null)
        {
            return NotFound();
        }

        return await EditViewAsync(new GameFormViewModel
        {
            Id = game.Id,
            Name = game.Name,
            ConsoleId = game.ConsoleId,
            Copies = game.Copies,
            OnWishList = game.IsOnWishList,
        }, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, GameFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await EditViewAsync(form, cancellationToken);
        }

        if (!await games.UpdateGameAsync(id, User.GetUserId(), form.Name, form.Copies, form.ConsoleId, form.OnWishList, cancellationToken))
        {
            return NotFound();
        }
        return RedirectToList(form.OnWishList);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var game = await games.DeleteGameAsync(id, User.GetUserId(), cancellationToken);
        if (game == null)
        {
            return NotFound();
        }
        return RedirectToList(game.IsOnWishList);
    }

    private async Task<ViewResult> ListAsync(bool onWishList, string? search, CancellationToken cancellationToken)
    {
        var ownGames = await games.GetGamesAsync(User.GetUserId(), onWishList, cancellationToken);
        return View("Index", new GameListViewModel
        {
            Games = SearchFilter.Filter(ownGames, search, g => g.Name),
            Search = search,
            IsWishList = onWishList,
        });
    }

    private async Task<ViewResult> EditViewAsync(GameFormViewModel form, CancellationToken cancellationToken)
    {
        form.ConsoleOptions = (await consoles.GetConsolesAsync(cancellationToken))
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        return View("Edit", form);
    }

    private RedirectToActionResult RedirectToList(bool onWishList)
    {
        return RedirectToAction(onWishList ? nameof(Wishlist) : nameof(Index));
    }
}
