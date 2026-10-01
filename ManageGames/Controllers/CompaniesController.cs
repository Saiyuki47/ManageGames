using ManageGames.Auth;
using ManageGames.Helpers;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManageGames.Controllers;

/// <summary>Companies are shared by all users, so only admins may manage them.</summary>
[Authorize(Roles = Roles.Admin)]
public class CompaniesController(CompanyService companies) : Controller
{
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        return View(new CompanyListViewModel
        {
            Companies = SearchFilter.Filter(await companies.GetCompaniesAsync(cancellationToken), search, c => c.Name),
            Search = search,
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("Edit", new CompanyFormViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CompanyFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", form);
        }

        await companies.AddCompanyAsync(form.Name, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var company = await companies.GetCompanyAsync(id, cancellationToken);
        if (company == null)
        {
            return NotFound();
        }

        return View(new CompanyFormViewModel { Id = company.Id, Name = company.Name });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CompanyFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        if (!await companies.UpdateCompanyAsync(id, form.Name, cancellationToken))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!await companies.DeleteCompanyAsync(id, cancellationToken))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }
}
