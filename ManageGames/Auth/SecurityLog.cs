namespace ManageGames.Auth;

/// <summary>
/// Security events (OWASP: log authentication and account changes), as source-generated log messages.
/// Never log passwords; the one exception is the generated one-time admin password, which exists
/// precisely to be read from the log once and has to be changed at the first login.
/// </summary>
public static partial class SecurityLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "User '{Username}' logged in from {RemoteIp}.")]
    public static partial void LoginSucceeded(this ILogger logger, string username, string? remoteIp);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Failed login for '{Username}' from {RemoteIp}.")]
    public static partial void LoginFailed(this ILogger logger, string username, string? remoteIp);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Login for '{Username}' from {RemoteIp} refused: the account is locked after too many failed attempts.")]
    public static partial void LoginLockedOut(this ILogger logger, string username, string? remoteIp);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Rate limit hit on {Path} by {RemoteIp}.")]
    public static partial void RateLimited(this ILogger logger, string path, string? remoteIp);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "User '{Username}' logged out.")]
    public static partial void LoggedOut(this ILogger logger, string? username);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "User '{Username}' changed their password.")]
    public static partial void PasswordChanged(this ILogger logger, string? username);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Warning, Message = "User '{Username}' entered a wrong current password while changing it.")]
    public static partial void PasswordChangeRejected(this ILogger logger, string? username);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "The password of '{Username}' no longer meets the policy; they have to change it.")]
    public static partial void PasswordBelowPolicy(this ILogger logger, string username);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "Admin '{Admin}' created user '{Username}' (admin: {IsAdmin}).")]
    public static partial void UserCreated(this ILogger logger, string? admin, string username, bool isAdmin);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Information, Message = "Admin '{Admin}' reset the password of '{Username}'.")]
    public static partial void PasswordReset(this ILogger logger, string? admin, string username);

    [LoggerMessage(EventId = 1022, Level = LogLevel.Information, Message = "Admin '{Admin}' deleted user '{Username}'.")]
    public static partial void UserDeleted(this ILogger logger, string? admin, string username);

    [LoggerMessage(EventId = 1030, Level = LogLevel.Warning, Message = "Created the initial admin account '{Username}' with the one-time password '{Password}'. You will be asked to choose your own password after logging in.")]
    public static partial void InitialAdminCreated(this ILogger logger, string username, string password);

    [LoggerMessage(EventId = 1031, Level = LogLevel.Warning, Message = "Hashed the plaintext passwords of {Count} user(s). They have to choose a new password at their next login.")]
    public static partial void PlaintextPasswordsHashed(this ILogger logger, int count);
}
