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

/// <summary>User administration, the first-start setup and password recovery, on top of ASP.NET Core Identity.</summary>
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

        var (result, oneTimePassword) = await WithOneTimePasswordAsync(
            password => CreateUserAsync(username, password, isAdmin: true, mustChangePassword: true));
        ThrowIfFailed(result, "Creating the initial admin failed");
        logger.InitialAdminCreated(username, oneTimePassword);
    }

    /// <summary>
    /// Recovery for when nobody can log in anymore, e.g. because the only admin forgot their password
    /// (<c>reset-password &lt;username&gt;</c> on the command line). Gives the account a random one-time
    /// password and logs it once; like an admin's reset, it lifts a lockout, ends all sessions and the
    /// user has to choose a new password at the next login. Returns null when the user doesn't exist.
    /// </summary>
    public async Task<string?> ResetToOneTimePasswordAsync(string username)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            logger.PasswordResetUserNotFound(username);
            return null;
        }

        var (result, oneTimePassword) = await WithOneTimePasswordAsync(password => ResetPasswordAsync(user, password));
        ThrowIfFailed(result, "Resetting the password failed");
        logger.OneTimePasswordIssued(user.UserName!, oneTimePassword);
        return oneTimePassword;
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

    // Generates random passwords until one passes the password rules (a random one is rarely guessable).
    private static async Task<(IdentityResult Result, string Password)> WithOneTimePasswordAsync(Func<string, Task<IdentityResult>> apply)
    {
        while (true)
        {
            var password = RandomNumberGenerator.GetString(PasswordAlphabet, GeneratedPasswordLength);
            var result = await apply(password);
            if (!result.Errors.Any(e => e.Code == CommonPasswordValidator.ErrorCode))
            {
                return (result, password);
            }
        }
    }

    private static void ThrowIfFailed(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{message}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }
    }

}
