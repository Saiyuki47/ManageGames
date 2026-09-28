using ManageGames.Data;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services
{
    /// <summary>
    /// Companies (console makers) are shared by all users. Only admins may change them, which the
    /// controller enforces.
    /// </summary>
    public class CompanyService
    {
        private readonly AppDbContext _db;

        public CompanyService(AppDbContext db)
        {
            _db = db;
        }

        public List<CompanyModel> GetCompanies()
        {
            return _db.Companies
                .OrderBy(c => EF.Functions.Collate(c.CompanyName, "NOCASE"))
                .AsNoTracking()
                .ToList();
        }

        public CompanyModel? GetCompany(int id)
        {
            return _db.Companies
                .AsNoTracking()
                .FirstOrDefault(c => c.CompanyId == id);
        }

        public void AddCompany(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            _db.Companies.Add(new CompanyModel { CompanyName = name.Trim() });
            _db.SaveChanges();
        }

        public bool UpdateCompany(int id, string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var company = _db.Companies.Find(id);
            if (company == null)
            {
                return false;
            }

            company.CompanyName = name.Trim();
            _db.SaveChanges();
            return true;
        }

        public bool DeleteCompany(int id)
        {
            var company = _db.Companies.Find(id);
            if (company == null)
            {
                return false;
            }

            // Consoles made by this company keep existing: the database sets their CompanyId to
            // NULL (ON DELETE SET NULL).
            _db.Companies.Remove(company);
            _db.SaveChanges();
            return true;
        }
    }
}
