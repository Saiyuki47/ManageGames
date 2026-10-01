using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ManageGames.Data;

/// <summary>
/// Lets the EF Core command-line tooling (migrations) build the context without starting the whole web
/// host. Adding migrations and checking for model changes never connects, so the connection string only
/// has to select the provider; the real one is configured in Program.cs at runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=managegames")
            .Options;

        return new AppDbContext(options);
    }
}
