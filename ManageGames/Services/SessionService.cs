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
    public string Start(Guid userId)
    {
        var now = DateTime.UtcNow;
        // Housekeeping: sessions past their absolute end can never be used again.
        db.UserSessions.Where(s => s.ExpiresAt <= now).ExecuteDelete();

        var session = new UserSession
        {
            Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now + AbsoluteLifetime,
        };
        db.UserSessions.Add(session);
        db.SaveChanges();
        return session.Id;
    }

    public bool IsActive(string sessionId, Guid userId)
    {
        var now = DateTime.UtcNow;
        return db.UserSessions.Any(s => s.Id == sessionId && s.UserId == userId && s.ExpiresAt > now);
    }

    /// <summary>Ends one session, e.g. when that browser logs out.</summary>
    public void End(string sessionId)
    {
        db.UserSessions.Where(s => s.Id == sessionId).ExecuteDelete();
    }

    /// <summary>Ends every session of the user, on all devices.</summary>
    public void EndAll(Guid userId)
    {
        db.UserSessions.Where(s => s.UserId == userId).ExecuteDelete();
    }
}
