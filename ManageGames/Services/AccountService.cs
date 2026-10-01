using System.Security.Claims;
using ManageGames.Models;
using Microsoft.AspNetCore.Identity;

namespace ManageGames.Services;

public enum LoginResult
{
    Succeeded,
    Failed,
    LockedOut,
}

/// <summary>The signed-in user's own account: logging in and out, changing the password.</summary>
public class AccountService(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    SessionService sessions)
{
    // Verifying against this hash when no password check happens (unknown user, locked account) makes
    // that failure take as long as a wrong password, so response times don't reveal which usernames
    // exist. Identity's own sign-in returns early in these cases.
    private static string? _timingDummyHash;

    public async Task<LoginResult> PasswordSignInAsync(string? username, string? password)
    {
        password ??= string.Empty;
        var user = string.IsNullOrWhiteSpace(username) ? null : await userManager.FindByNameAsync(username);
        if (user == null)
        {
            SpendHashingTime(password);
            return LoginResult.Failed;
        }

        // Counts failures per account: after too many, the account is locked for a while.
        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            SpendHashingTime(password);
            return LoginResult.LockedOut;
        }
        if (!result.Succeeded)
        {
            return LoginResult.Failed;
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return LoginResult.Succeeded;
    }

    /// <summary>Signs out this browser only; the user's other devices stay signed in.</summary>
    public Task SignOutAsync()
    {
        return signInManager.SignOutAsync();
    }

    /// <summary>
    /// Changes the signed-in user's password. On success every other session ends and this browser
    /// continues with a new session.
    /// </summary>
    public async Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal principal, string currentPassword, string newPassword)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            return IdentityResult.Failed(userManager.ErrorDescriber.PasswordMismatch());
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await sessions.EndAllAsync(user.Id);
        await signInManager.SignInAsync(user, isPersistent: false);
        return result;
    }


    private void SpendHashingTime(string password)
    {
        var dummy = new AppUser();
        _timingDummyHash ??= userManager.PasswordHasher.HashPassword(dummy, "timing-dummy");
        userManager.PasswordHasher.VerifyHashedPassword(dummy, _timingDummyHash, password);
    }
}
