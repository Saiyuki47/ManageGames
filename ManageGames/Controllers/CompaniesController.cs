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
    public IActionResult Index(string? search)
    {
        return View(new CompanyListViewModel
        {
            Companies = SearchFilter.Filter(companies.GetCompanies(), search, c => c.Name),
            Search = search,
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View("Edit", new CompanyFormViewModel());
    }

    [HttpPost]
    public IActionResult Create(CompanyFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", form);
        }

        companies.AddCompany(form.Name);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var company = companies.GetCompany(id);
        if (company == null)
        {
            return NotFound();
        }

        return View(new CompanyFormViewModel { Id = company.Id, Name = company.Name });
    }

    [HttpPost]
    public IActionResult Edit(int id, CompanyFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        if (!companies.UpdateCompany(id, form.Name))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!companies.DeleteCompany(id))
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }
}
