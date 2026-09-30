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
    public IActionResult Index(string? search)
    {
        return View(new ConsoleListViewModel
        {
            Consoles = SearchFilter.Filter(consoles.GetConsoles(), search, c => c.Name),
            Search = search,
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return EditView(new ConsoleFormViewModel());
    }

    [HttpPost]
    public IActionResult Create(ConsoleFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return EditView(form);
        }

        consoles.AddConsole(form.Name, form.CompanyId);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var console = consoles.GetConsole(id);
        if (console == null)
        {
            return NotFound();
        }

        return EditView(new ConsoleFormViewModel { Id = console.Id, Name = console.Name, CompanyId = console.CompanyId });
    }

    [HttpPost]
    public IActionResult Edit(int id, ConsoleFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return EditView(form);
        }

        if (!consoles.UpdateConsole(id, form.Name, form.CompanyId))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!consoles.DeleteConsole(id))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    private ViewResult EditView(ConsoleFormViewModel form)
    {
        form.CompanyOptions = companies.GetCompanies()
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        return View("Edit", form);
    }
}
