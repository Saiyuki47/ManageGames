using ManageGames.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ManageGames.Data;

/// <summary>The app's data plus the ASP.NET Core Identity tables (AspNetUsers, AspNetRoles, ...).</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<GameConsole> Consoles => Set<GameConsole>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<GameConsole>()
            .HasOne(c => c.Company)
            .WithMany()
            .HasForeignKey(c => c.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Game>(e =>
        {
            e.HasOne(g => g.Console)
             .WithMany()
             .HasForeignKey(g => g.ConsoleId)
             .OnDelete(DeleteBehavior.SetNull);
            // Deleting a user deletes their games along with them.
            e.HasOne(g => g.User)
             .WithMany()
             .HasForeignKey(g => g.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserSession>(e =>
        {
            e.HasOne<AppUser>()
             .WithMany()
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.UserId);
        });

        // Every ITimestamped entity gets a DB-side default so rows inserted outside EF
        // (or backfilled by a migration) still receive a sensible timestamp.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITimestamped).IsAssignableFrom(entityType.ClrType))
            {
                var entity = builder.Entity(entityType.ClrType);
                entity.Property(nameof(ITimestamped.CreatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(nameof(ITimestamped.UpdatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All timestamps are stored in UTC. SQLite has no date type, so without this they would be
        // read back with an unspecified kind and could not be converted to local time for display.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    // Override the most-derived overloads: the parameterless SaveChanges()/SaveChangesAsync()
    // funnel through these, so the timestamp hook cannot be sidestepped.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Sets CreatedAt/UpdatedAt automatically so services never have to touch them.
    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
