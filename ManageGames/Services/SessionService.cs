using System.Security.Cryptography;
using ManageGames.Data;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services;

/// <summary>
/// Server-side sessions: each sign-in gets a random id that the auth cookie carries and that is checked on
/// every request. Ending a session revokes that one cookie (and every copy of it) on the server.
/// </summary>
public class SessionService(AppDbContext db, IConfiguration configuration)
{
    /// <summary>How long a session may last at most, however active it is (OWASP: absolute timeout).</summary>
    public static readonly TimeSpan DefaultAbsoluteLifetime = TimeSpan.FromHours(12);

    private TimeSpan AbsoluteLifetime =>
        configuration.GetValue("Authentication:AbsoluteSessionLifetime", DefaultAbsoluteLifetime);

    /// <summary>Starts a session for the user and returns its id.</summary>
    public async Task<string> StartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        // Housekeeping: sessions past their absolute end can never be used again.
        await db.UserSessions.Where(s => s.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

        var session = new UserSession
        {
            Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now + AbsoluteLifetime,
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    public Task<bool> IsActiveAsync(string sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return db.UserSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.ExpiresAt > now, cancellationToken);
    }

    /// <summary>Ends one session, e.g. when that browser logs out.</summary>
    public Task EndAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return db.UserSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Ends every session of the user, on all devices.</summary>
    public Task EndAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return db.UserSessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
