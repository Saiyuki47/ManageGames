using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<CompanyModel> Companies => Set<CompanyModel>();
        public DbSet<ConsoleModel> Consoles => Set<ConsoleModel>();
        public DbSet<UserModel> Users => Set<UserModel>();
        public DbSet<GameModel> Games => Set<GameModel>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Table names keep the original tbl* prefix; columns use consistent English names.
            modelBuilder.Entity<CompanyModel>(e =>
            {
                e.ToTable("tblCompanies");
                e.HasKey(c => c.CompanyId);
                e.Property(c => c.CompanyId).HasColumnName("ID");
                e.Property(c => c.CompanyName).HasColumnName("CompanyName");
            });

            modelBuilder.Entity<ConsoleModel>(e =>
            {
                e.ToTable("tblConsoles");
                e.HasKey(c => c.ConsoleId);
                e.Property(c => c.ConsoleId).HasColumnName("ID");
                e.Property(c => c.ConsoleName).HasColumnName("ConsoleName");
                e.Property(c => c.CompanyId).HasColumnName("Company");
                e.HasOne(c => c.Company)
                 .WithMany()
                 .HasForeignKey(c => c.CompanyId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<UserModel>(e =>
            {
                e.ToTable("tblUser");
                e.HasKey(u => u.UserID);
                e.Property(u => u.UserID).HasColumnName("UserID").ValueGeneratedNever();
                e.Property(u => u.ProfilePicturesID).HasColumnName("ProfilePicturesID");
                e.Property(u => u.Username).HasColumnName("Username");
                e.Property(u => u.NormalizedUsername).HasColumnName("NormalizedUsername");
                e.Property(u => u.Password).HasColumnName("Password");
                e.Property(u => u.IsAdmin).HasColumnName("IsAdmin");
                e.Property(u => u.CookieID).HasColumnName("CookieID");
                e.HasIndex(u => u.NormalizedUsername).IsUnique();
            });

            modelBuilder.Entity<GameModel>(e =>
            {
                e.ToTable("tblGames");
                e.HasKey(g => g.GameId);
                e.Property(g => g.GameId).HasColumnName("ID");
                e.Property(g => g.ConsoleId).HasColumnName("Console");
                e.Property(g => g.GameName).HasColumnName("GameName");
                e.Property(g => g.Copies).HasColumnName("Copies");
                e.Property(g => g.IsOnWishList).HasColumnName("IsOnWishList");
                e.Property(g => g.UserId).HasColumnName("UserID");
                e.HasOne(g => g.Console)
                 .WithMany()
                 .HasForeignKey(g => g.ConsoleId)
                 .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(g => g.User)
                 .WithMany()
                 .HasForeignKey(g => g.UserId);
            });

            // Every ITimestamped entity gets a DB-side default so rows inserted outside EF
            // (or backfilled by a migration) still receive a sensible timestamp.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITimestamped).IsAssignableFrom(entityType.ClrType))
                {
                    var entity = modelBuilder.Entity(entityType.ClrType);
                    entity.Property(nameof(ITimestamped.CreatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
                    entity.Property(nameof(ITimestamped.UpdatedAt)).HasDefaultValueSql("CURRENT_TIMESTAMP");
                }
            }
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
}
