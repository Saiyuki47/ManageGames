using ManageGames.Data;
using ManageGames.Helpers;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services;

/// <summary>
/// Consoles are shared by all users. Only admins may change them, which the controller enforces.
/// </summary>
public class ConsoleService(AppDbContext db)
{
    public List<GameConsole> GetConsoles()
    {
        return db.Consoles
            .Include(c => c.Company)
            .AsNoTracking()
            .AsEnumerable()
            .OrderBy(c => c.Name, NameOrder.Comparer)
            .ToList();
    }

    public GameConsole? GetConsole(int id)
    {
        return db.Consoles
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);
    }

    public void AddConsole(string name, int? companyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        db.Consoles.Add(new GameConsole { Name = name.Trim(), CompanyId = ExistingCompanyId(companyId) });
        db.SaveChanges();
    }

    public bool UpdateConsole(int id, string name, int? companyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var console = db.Consoles.Find(id);
        if (console == null)
        {
            return false;
        }

        console.Name = name.Trim();
        console.CompanyId = ExistingCompanyId(companyId);
        db.SaveChanges();
        return true;
    }

    public bool DeleteConsole(int id)
    {
        var console = db.Consoles.Find(id);
        if (console == null)
        {
            return false;
        }

        // Games referencing this console keep existing: the database sets their ConsoleId to
        // NULL (ON DELETE SET NULL).
        db.Consoles.Remove(console);
        db.SaveChanges();
        return true;
    }

    // A company id that no longer exists degrades to "no company" instead of failing on the foreign key.
    private int? ExistingCompanyId(int? id)
    {
        return id.HasValue && db.Companies.Any(c => c.Id == id.Value) ? id : null;
    }
}
