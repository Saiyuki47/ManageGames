using System.Globalization;
using ManageGames.Auth;
using ManageGames.Helpers;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.Controllers;

/// <summary>The signed-in user's own game collection and wishlist. The covers are handled by <see cref="CoversController"/>.</summary>
[Authorize]
public class GamesController(GameService games, ConsoleService consoles, CoverService covers) : Controller
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

        var userId = User.GetUserId();
        var gameId = await games.AddGameAsync(userId, form.Name, form.Copies, form.ConsoleId, form.OnWishList, cancellationToken);
        // Waits for the cover (within a time limit), so the list shows it right away.
        await covers.FindMissingCoverAsync(gameId, userId, cancellationToken);
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

        var userId = User.GetUserId();
        var before = await games.GetGameAsync(id, userId, cancellationToken);
        if (before == null || !await games.UpdateGameAsync(id, userId, form.Name, form.Copies, form.ConsoleId, form.OnWishList, cancellationToken))
        {
            return NotFound();
        }

        // A corrected title or console may find the cover the first search missed.
        if (!string.Equals(before.Name, form.Name.Trim(), StringComparison.Ordinal) || before.ConsoleId != form.ConsoleId)
        {
            await covers.FindMissingCoverAsync(id, userId, cancellationToken);
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
        if (form.Id is { } id)
        {
            form.Cover = await covers.GetCoverInfoAsync(id, User.GetUserId(), cancellationToken);
        }
        return View("Edit", form);
    }

    private RedirectToActionResult RedirectToList(bool onWishList)
    {
        return RedirectToAction(onWishList ? nameof(Wishlist) : nameof(Index));
    }
}
