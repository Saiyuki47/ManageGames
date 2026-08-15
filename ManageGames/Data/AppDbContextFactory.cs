using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ManageGames.Data
{
    /// <summary>
    /// Lets the EF Core command-line tooling (migrations) build the context without
    /// starting the whole web host. The connection string only needs to identify the
    /// provider here; the real path is configured in Program.cs at runtime.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Data Source=DB/DataBase.db")
                .Options;

            return new AppDbContext(options);
        }
    }
}
