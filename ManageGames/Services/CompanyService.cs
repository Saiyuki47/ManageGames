using ManageGames.Data;
using ManageGames.Helpers;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services;

/// <summary>
/// Companies (console makers) are shared by all users. Only admins may change them, which the
/// controller enforces.
/// </summary>
public class CompanyService(AppDbContext db)
{
    public List<Company> GetCompanies()
    {
        return db.Companies
            .AsNoTracking()
            .AsEnumerable()
            .OrderBy(c => c.Name, NameOrder.Comparer)
            .ToList();
    }

    public Company? GetCompany(int id)
    {
        return db.Companies
            .AsNoTracking()
            .FirstOrDefault(c => c.Id == id);
    }

    public void AddCompany(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        db.Companies.Add(new Company { Name = name.Trim() });
        db.SaveChanges();
    }

    public bool UpdateCompany(int id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var company = db.Companies.Find(id);
        if (company == null)
        {
            return false;
        }

        company.Name = name.Trim();
        db.SaveChanges();
        return true;
    }

    public bool DeleteCompany(int id)
    {
        var company = db.Companies.Find(id);
        if (company == null)
        {
            return false;
        }

        // Consoles made by this company keep existing: the database sets their CompanyId to
        // NULL (ON DELETE SET NULL).
        db.Companies.Remove(company);
        db.SaveChanges();
        return true;
    }
}
