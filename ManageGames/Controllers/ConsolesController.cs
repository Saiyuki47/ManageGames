using System.Globalization;
using ManageGames.Auth;
using ManageGames.Helpers;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.Controllers;

/// <summary>Consoles are shared by all users, so only admins may manage them.</summary>
[Authorize(Roles = Roles.Admin)]
public class ConsolesController(ConsoleService consoles, CompanyService companies) : Controller
{
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        return View(new ConsoleListViewModel
        {
            Consoles = SearchFilter.Filter(await consoles.GetConsolesAsync(cancellationToken), search, c => c.Name),
            Search = search,
        });
    }

    [HttpGet]
    public Task<ViewResult> Create(CancellationToken cancellationToken)
    {
        return EditViewAsync(new ConsoleFormViewModel(), cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ConsoleFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await EditViewAsync(form, cancellationToken);
        }

        await consoles.AddConsoleAsync(form.Name, form.CompanyId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var console = await consoles.GetConsoleAsync(id, cancellationToken);
        if (console == null)
        {
            return NotFound();
        }

        return await EditViewAsync(new ConsoleFormViewModel { Id = console.Id, Name = console.Name, CompanyId = console.CompanyId }, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ConsoleFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await EditViewAsync(form, cancellationToken);
        }

        if (!await consoles.UpdateConsoleAsync(id, form.Name, form.CompanyId, cancellationToken))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!await consoles.DeleteConsoleAsync(id, cancellationToken))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<ViewResult> EditViewAsync(ConsoleFormViewModel form, CancellationToken cancellationToken)
    {
        form.CompanyOptions = (await companies.GetCompaniesAsync(cancellationToken))
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        return View("Edit", form);
    }
}
