using ManageGames.Data;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services;

/// <summary>
/// Companies (console makers) are shared by all users. Only admins may change them, which the
/// controller enforces.
/// </summary>
public class CompanyService(AppDbContext db)
{
    public async Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Companies
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Company?> GetCompanyAsync(int id, CancellationToken cancellationToken = default)
    {
        return db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddCompanyAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        db.Companies.Add(new Company { Name = name.Trim() });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateCompanyAsync(int id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var company = await db.Companies.FindAsync([id], cancellationToken);
        if (company == null)
        {
            return false;
        }

        company.Name = name.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteCompanyAsync(int id, CancellationToken cancellationToken = default)
    {
        var company = await db.Companies.FindAsync([id], cancellationToken);
        if (company == null)
        {
            return false;
        }

        // Consoles made by this company keep existing: the database sets their CompanyId to
        // NULL (ON DELETE SET NULL).
        db.Companies.Remove(company);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
