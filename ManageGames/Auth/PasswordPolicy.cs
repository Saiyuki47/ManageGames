namespace ManageGames.Auth;

/// <summary>Password rules, following NIST SP 800-63B-4 and the OWASP Password Storage Cheat Sheet.</summary>
public static class PasswordPolicy
{
    /// <summary>NIST SP 800-63B-4 requires 15 characters for passwords used without a second factor.</summary>
    public const int MinLength = 15;

    /// <summary>NIST asks to allow at least 64 characters; the cap only limits hashing work.</summary>
    public const int MaxLength = 128;

    /// <summary>Guards against repetitive passwords like "abababab…".</summary>
    public const int MinUniqueChars = 5;

    /// <summary>OWASP's recommendation for PBKDF2-HMAC-SHA512, the algorithm of Identity's hasher.</summary>
    public const int HashIterationCount = 210_000;
}
