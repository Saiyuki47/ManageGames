using ManageGames.Auth;
using ManageGames.Helpers;
using ManageGames.Services;
using ManageGames.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ManageGames.Controllers
{
    /// <summary>Consoles are shared by all users, so only admins may manage them.</summary>
    [Authorize(Roles = Roles.Admin)]
    public class ConsolesController : Controller
    {
        private readonly ConsoleService _consoles;
        private readonly CompanyService _companies;

        public ConsolesController(ConsoleService consoles, CompanyService companies)
        {
            _consoles = consoles;
            _companies = companies;
        }

        public IActionResult Index(string? search)
        {
            return View(new ConsoleListViewModel
            {
                Consoles = SearchFilter.Filter(_consoles.GetConsoles(), search, c => c.ConsoleName),
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

            _consoles.AddConsole(form.Name, form.CompanyId);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var console = _consoles.GetConsole(id);
            if (console == null)
            {
                return NotFound();
            }

            return EditView(new ConsoleFormViewModel { Id = console.ConsoleId, Name = console.ConsoleName, CompanyId = console.CompanyId });
        }

        [HttpPost]
        public IActionResult Edit(int id, ConsoleFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                return EditView(form);
            }

            if (!_consoles.UpdateConsole(id, form.Name, form.CompanyId))
            {
                return NotFound();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            if (!_consoles.DeleteConsole(id))
            {
                return NotFound();
            }
            return RedirectToAction(nameof(Index));
        }

        private IActionResult EditView(ConsoleFormViewModel form)
        {
            form.CompanyOptions = _companies.GetCompanies()
                .Select(c => new SelectListItem(c.CompanyName, c.CompanyId.ToString()))
                .ToList();
            return View("Edit", form);
        }
    }
}
