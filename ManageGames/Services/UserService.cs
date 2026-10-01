using System.Security.Cryptography;
using ManageGames.Auth;
using ManageGames.Data;
using ManageGames.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ManageGames.Services;

public enum DeleteUserResult
{
    Deleted,
    NotFound,
    CannotDeleteSelf,
}

public record UserListItem(Guid Id, string UserName, bool IsAdmin, bool MustChangePassword, DateTime CreatedAt);

/// <summary>User administration and the first-start setup, on top of ASP.NET Core Identity.</summary>
public class UserService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    SessionService sessions,
    ILogger<UserService> logger)
{
    private const string DefaultAdminUsername = "admin";
    // Characters for generated passwords; look-alikes (0/O, 1/l/I) are left out.
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private const int GeneratedPasswordLength = 20;

    /// <summary>
    /// Creates the admin role and, on an empty database, the first admin. Without a configured password
    /// a random one-time password is generated and logged; it has to be changed at the first login.
    /// </summary>
    public async Task EnsureInitialAdminAsync(string? configuredUsername, string? configuredPassword)
    {
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
        {
            ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin)), "Creating the admin role failed");
        }
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var username = string.IsNullOrWhiteSpace(configuredUsername) ? DefaultAdminUsername : configuredUsername;
        if (!string.IsNullOrEmpty(configuredPassword))
        {
            ThrowIfFailed(await CreateUserAsync(username, configuredPassword, isAdmin: true, mustChangePassword: false),
                "Seed:AdminPassword is not accepted");
            return;
        }

        IdentityResult result;
        string oneTimePassword;
        do
        {
            oneTimePassword = RandomNumberGenerator.GetString(PasswordAlphabet, GeneratedPasswordLength);
            result = await CreateUserAsync(username, oneTimePassword, isAdmin: true, mustChangePassword: true);
        }
        while (result.Errors.Any(e => e.Code == CommonPasswordValidator.ErrorCode));
        ThrowIfFailed(result, "Creating the initial admin failed");
        logger.InitialAdminCreated(username, oneTimePassword);
    }

    /// <summary>
    /// Versions before the EF Core rewrite stored passwords in plaintext, which the hasher can't
    /// verify (it throws on values that aren't Base64). Hashes them once; since the plaintext may
    /// have been exposed, those users have to choose a new password and their sessions end.
    /// </summary>
    public async Task HashPlaintextPasswordsAsync()
    {
        var users = (await db.Users.ToListAsync())
            .Where(u => !string.IsNullOrEmpty(u.PasswordHash) && !IsPasswordHash(u.PasswordHash))
            .ToList();
        foreach (var user in users)
        {
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, user.PasswordHash!);
            user.MustChangePassword = true;
            await userManager.UpdateSecurityStampAsync(user);
        }
        if (users.Count > 0)
        {
            logger.PlaintextPasswordsHashed(users.Count);
        }
    }

    public Task<IdentityResult> CreateUserAsync(string username, string password, bool isAdmin, bool mustChangePassword)
    {
        // Retries after transient connection failures must repeat the whole transaction.
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var user = new AppUser { UserName = username.Trim(), MustChangePassword = mustChangePassword };

            // Creating the account and granting the role succeed or fail together.
            await using var transaction = await db.Database.BeginTransactionAsync();
            IdentityResult result;
            try
            {
                result = await userManager.CreateAsync(user, password);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Identity checks for duplicates first, but a concurrent request can still take the name
                // before the insert; the unique index then rejects it.
                db.ChangeTracker.Clear();
                return IdentityResult.Failed(userManager.ErrorDescriber.DuplicateUserName(user.UserName));
            }
            if (result.Succeeded && isAdmin)
            {
                result = await userManager.AddToRoleAsync(user, Roles.Admin);
            }
            if (result.Succeeded)
            {
                await transaction.CommitAsync();
            }
            return result;
        });
    }

    /// <summary>
    /// Sets a temporary password chosen by an admin; the user has to replace it at the next login.
    /// Also lifts a lockout and signs the user out everywhere.
    /// </summary>
    public async Task<IdentityResult> ResetPasswordAsync(AppUser user, string temporaryPassword)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        user.MustChangePassword = true;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await sessions.EndAllAsync(user.Id);
        return result;
    }

    public async Task<(DeleteUserResult Result, string? Username)> DeleteUserAsync(Guid userId, Guid actingUserId)
    {
        // Admins can't delete their own account, which also guarantees that one admin always remains.
        if (userId == actingUserId)
        {
            return (DeleteUserResult.CannotDeleteSelf, null);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (DeleteUserResult.NotFound, null);
        }

        // Their games and sessions are deleted along with them (cascading foreign keys).
        ThrowIfFailed(await userManager.DeleteAsync(user), "Deleting the user failed");
        return (DeleteUserResult.Deleted, user.UserName);
    }

    public Task<AppUser?> GetUserAsync(Guid userId)
    {
        return userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<List<UserListItem>> GetUsersAsync()
    {
        var adminIds = (await userManager.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();
        return users
            .Select(u => new UserListItem(u.Id, u.UserName!, adminIds.Contains(u.Id), u.MustChangePassword, u.CreatedAt))
            .ToList();
    }

    private static void ThrowIfFailed(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{message}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }

    // PasswordHasher output is Base64 with a format marker: 0x00 for the Identity v2 format (always
    // 49 bytes) or 0x01 for v3 (a 13-byte header followed by salt and subkey).
    private static bool IsPasswordHash(string value)
    {
        var bytes = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, bytes, out var length) || length == 0)
        {
            return false;
        }
        return (bytes[0] == 0x00 && length == 49) || (bytes[0] == 0x01 && length > 13);
    }
}
