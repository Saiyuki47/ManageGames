using ManageGames.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Data;

/// <summary>
/// The app's data plus the ASP.NET Core Identity tables (AspNetUsers, AspNetRoles, ...) and the keys that
/// encrypt the cookies, in PostgreSQL.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>
    /// Collation of names: Unicode-aware and ordered like a dictionary, so "Ökami" sorts next to "Okami"
    /// and case doesn't decide the order. Equality stays exact (deterministic), so LIKE and indexes work.
    /// </summary>
    public const string NameCollation = "names";

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<GameConsole> Consoles => Set<GameConsole>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameCover> GameCovers => Set<GameCover>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    // Shared by all instances of the app, so a cookie issued by one instance is accepted by the others.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasCollation(NameCollation, locale: "und", provider: "icu", deterministic: true);
        builder.Entity<AppUser>().Property(u => u.UserName).UseCollation(NameCollation);
        builder.Entity<Company>().Property(c => c.Name).UseCollation(NameCollation);

        builder.Entity<GameConsole>(e =>
        {
            e.Property(c => c.Name).UseCollation(NameCollation);
            e.HasOne(c => c.Company)
             .WithMany()
             .HasForeignKey(c => c.CompanyId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Game>(e =>
        {
            e.Property(g => g.Name).UseCollation(NameCollation);
            e.HasOne(g => g.Console)
             .WithMany()
             .HasForeignKey(g => g.ConsoleId)
             .OnDelete(DeleteBehavior.SetNull);
            // Deleting a user deletes their games along with them.
            e.HasOne(g => g.User)
             .WithMany()
             .HasForeignKey(g => g.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            // The game lists filter by owner and wishlist flag.
            e.HasIndex(g => new { g.UserId, g.IsOnWishList });
        });

        ConfigureCovers(builder);

        builder.Entity<UserSession>(e =>
        {
            e.HasOne<AppUser>()
             .WithMany()
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => s.ExpiresAt);
        });

        // Every ITimestamped entity gets a DB-side default so rows inserted outside EF still receive a
        // sensible timestamp.
        foreach (var entityType in builder.Model.GetEntityTypes().Where(t => typeof(ITimestamped).IsAssignableFrom(t.ClrType)))
        {
            var entity = builder.Entity(entityType.ClrType);
            entity.Property(nameof(ITimestamped.CreatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(nameof(ITimestamped.UpdatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }

    private static void ConfigureCovers(ModelBuilder builder)
    {
        builder.Entity<GameCover>(e =>
        {
            // At most one cover per game; it goes away with the game.
            e.HasKey(c => c.GameId);
            e.HasOne(c => c.Game)
             .WithOne(g => g.Cover)
             .HasForeignKey<GameCover>(c => c.GameId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(c => c.ContentType).HasMaxLength(50);
            e.Property(c => c.Source).HasMaxLength(50);
        });
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
}
