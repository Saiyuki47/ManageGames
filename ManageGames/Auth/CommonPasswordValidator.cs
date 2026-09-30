using ManageGames.Models;
using Microsoft.AspNetCore.Identity;

namespace ManageGames.Auth;

/// <summary>
/// The blocklist NIST SP 800-63B-4 asks for: rejects passwords that are easy to guess despite their
/// length, such as repetitions ("hallo123hallo123"), sequences ("123456789012345", "qwertzuiopasdfg"),
/// common words padded with digits ("password1234567") and the username or the app's name. It works
/// offline, so no password (or hash of one) ever leaves the machine.
/// </summary>
public sealed class CommonPasswordValidator : IPasswordValidator<AppUser>
{
    public const string ErrorCode = "CommonPassword";

    private static readonly string[] Sequences =
    [
        "0123456789",
        "abcdefghijklmnopqrstuvwxyz",
        // Keyboard rows (QWERTY and QWERTZ) and the "1qaz2wsx" columns.
        "qwertyuiopasdfghjklzxcvbnm",
        "qwertzuiopasdfghjklyxcvbnm",
        "1qaz2wsx3edc4rfv5tgb6yhn7ujm8ik9ol0p",
        "1qay2wsx3edc4rfv5tgb6zhn7ujm8ik9ol0p",
    ];

    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "password", "passwort", "passw", "pass", "qwerty", "qwertz", "asdf", "letmein", "welcome", "willkommen",
        "iloveyou", "ichliebedich", "admin", "administrator", "root", "user", "login", "test", "guest",
        "secret", "geheim", "changeme", "trustno", "dragon", "monkey", "master", "shadow", "superman", "batman",
        "football", "fussball", "baseball", "soccer", "sunshine", "princess", "schatz", "hallo", "hello",
        "summer", "sommer", "winter", "spring", "autumn", "herbst", "starwars", "pokemon", "minecraft",
        "nintendo", "playstation", "xbox", "zelda", "mario", "game", "games", "gamer", "gaming",
    };

    public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        return Task.FromResult(IsGuessable(password ?? string.Empty, user.UserName)
            ? IdentityResult.Failed(new IdentityError
            {
                Code = ErrorCode,
                Description = "This password is too easy to guess. Avoid repetitions, sequences like 12345 or qwertz, common words and your username.",
            })
            : IdentityResult.Success);
    }

    public static bool IsGuessable(string password, string? username)
    {
        var lower = password.ToLowerInvariant();
        var letters = new string(lower.Where(char.IsLetter).ToArray());

        return ContainsContextWord(lower, username)
            || IsRepetition(lower)
            || IsRepetition(letters)
            || IsSequence(lower)
            || (letters.Length > 0 && (CommonWords.Contains(letters) || CommonWords.Any(w => IsRepetitionOf(letters, w))));
    }

    private static bool ContainsContextWord(string password, string? username)
    {
        var name = username?.ToLowerInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        return password.Contains("managegames", StringComparison.Ordinal)
            || (name is { Length: >= 3 } && password.Contains(name, StringComparison.Ordinal));
    }

    // True when the whole value is one shorter unit repeated, e.g. "abcabcabc".
    private static bool IsRepetition(string value)
    {
        for (var unit = 1; unit <= value.Length / 2; unit++)
        {
            if (value.Length % unit == 0 && IsRepetitionOf(value, value[..unit]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsRepetitionOf(string value, string unit)
    {
        if (value.Length < unit.Length || value.Length % unit.Length != 0)
        {
            return false;
        }
        for (var i = 0; i < value.Length; i += unit.Length)
        {
            if (string.CompareOrdinal(value, i, unit, 0, unit.Length) != 0)
            {
                return false;
            }
        }
        return true;
    }

    // True when the value is a run of a sequence, forwards or backwards and wrapping around.
    private static bool IsSequence(string value)
    {
        foreach (var sequence in Sequences)
        {
            var repeats = (value.Length / sequence.Length) + 2;
            var forwards = string.Concat(Enumerable.Repeat(sequence, repeats));
            var backwards = new string(forwards.Reverse().ToArray());
            if (forwards.Contains(value, StringComparison.Ordinal) || backwards.Contains(value, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
