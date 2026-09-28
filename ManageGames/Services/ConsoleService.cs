using ManageGames.Data;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services
{
    /// <summary>
    /// Consoles are shared by all users. Only admins may change them, which the controller enforces.
    /// </summary>
    public class ConsoleService
    {
        private readonly AppDbContext _db;

        public ConsoleService(AppDbContext db)
        {
            _db = db;
        }

        public List<ConsoleModel> GetConsoles()
        {
            return _db.Consoles
                .Include(c => c.Company)
                .OrderBy(c => EF.Functions.Collate(c.ConsoleName, "NOCASE"))
                .AsNoTracking()
                .ToList();
        }

        public ConsoleModel? GetConsole(int id)
        {
            return _db.Consoles
                .AsNoTracking()
                .FirstOrDefault(c => c.ConsoleId == id);
        }

        public void AddConsole(string name, int? companyId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            _db.Consoles.Add(new ConsoleModel { ConsoleName = name.Trim(), CompanyId = ExistingCompanyId(companyId) });
            _db.SaveChanges();
        }

        public bool UpdateConsole(int id, string name, int? companyId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var console = _db.Consoles.Find(id);
            if (console == null)
            {
                return false;
            }

            console.ConsoleName = name.Trim();
            console.CompanyId = ExistingCompanyId(companyId);
            _db.SaveChanges();
            return true;
        }

        public bool DeleteConsole(int id)
        {
            var console = _db.Consoles.Find(id);
            if (console == null)
            {
                return false;
            }

            // Games referencing this console keep existing: the database sets their ConsoleId to
            // NULL (ON DELETE SET NULL).
            _db.Consoles.Remove(console);
            _db.SaveChanges();
            return true;
        }

        // A company id that no longer exists degrades to "no company" instead of failing on the foreign key.
        private int? ExistingCompanyId(int? id)
        {
            return id.HasValue && _db.Companies.Any(c => c.CompanyId == id.Value) ? id : null;
        }
    }
}
