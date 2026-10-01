using ManageGames.Data;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services;

/// <summary>
/// Consoles are shared by all users. Only admins may change them, which the controller enforces.
/// </summary>
public class ConsoleService(AppDbContext db)
{
    public Task<List<GameConsole>> GetConsolesAsync(CancellationToken cancellationToken = default)
    {
        return db.Consoles
            .Include(c => c.Company)
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<GameConsole?> GetConsoleAsync(int id, CancellationToken cancellationToken = default)
    {
        return db.Consoles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddConsoleAsync(string name, int? companyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        db.Consoles.Add(new GameConsole { Name = name.Trim(), CompanyId = await ExistingCompanyIdAsync(companyId, cancellationToken) });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateConsoleAsync(int id, string name, int? companyId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var console = await db.Consoles.FindAsync([id], cancellationToken);
        if (console == null)
        {
            return false;
        }

        console.Name = name.Trim();
        console.CompanyId = await ExistingCompanyIdAsync(companyId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteConsoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var console = await db.Consoles.FindAsync([id], cancellationToken);
        if (console == null)
        {
            return false;
        }

        // Games referencing this console keep existing: the database sets their ConsoleId to
        // NULL (ON DELETE SET NULL).
        db.Consoles.Remove(console);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // A company id that no longer exists degrades to "no company" instead of failing on the foreign key.
    private async Task<int?> ExistingCompanyIdAsync(int? id, CancellationToken cancellationToken)
    {
        return id.HasValue && await db.Companies.AnyAsync(c => c.Id == id.Value, cancellationToken) ? id : null;
    }
}
