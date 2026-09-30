# ManageGames

A self-hosted ASP.NET Core MVC app to keep track of your video game collection: games, consoles,
their makers and a wishlist. Every user sees only their own collection.

## Features

- Your own game library: add, edit and delete games, count copies, search.
- A wishlist for the games you still want.
- Consoles and the companies that make them, shared by all users and managed by admins.
- User administration for admins: create accounts, reset forgotten passwords, delete accounts.
- Everyone can change their own password.

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/Saiyuki47/ManageGames.git
cd ManageGames
dotnet run --project ManageGames
```

Then open <https://localhost:7200>. The first start creates the SQLite database
(`ManageGames/DB/DataBase.db`) and the admin account `admin`, and prints its one-time password to
the console. You choose your own password at the first login.

### Configuration

Settings can come from `appsettings.json`, user secrets or environment variables
(for example `Seed__AdminPassword`).

| Setting | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings:ManageGames` | SQLite connection string | `ManageGames/DB/DataBase.db` |
| `Seed:AdminUsername` | Name of the first admin, used only on an empty database | `admin` |
| `Seed:AdminPassword` | Password of the first admin (no forced change) | a generated one-time password |
| `RateLimiting:LoginPermitLimit` | Login attempts per minute and client IP | `10` |
| `RateLimiting:PasswordChangePermitLimit` | Password change attempts per minute and user | `10` |

## Security

- ASP.NET Core cookie authentication: the cookie is HttpOnly, Secure over HTTPS, `SameSite=Strict`
  and expires after two hours without activity.
- Sessions are also checked on the server: logging out and changing or resetting a password rotate
  a per-user security stamp, which makes every copy of an older cookie worthless.
- Games are scoped to their owner in every query; consoles, companies and users require the admin role.
- All forms are protected against CSRF, login and password change attempts are rate-limited, and passwords are stored as
  salted PBKDF2 hashes.

## Development

```bash
dotnet test                        # integration tests: in-memory host, throw-away SQLite databases
dotnet format ManageGames.sln      # code style (verified in CI)
dotnet tool restore                # once, for the pinned dotnet-ef
dotnet dotnet-ef migrations add <Name> --project ManageGames   # after changing the model
```

The CI workflow builds with warnings treated as errors, runs the tests, audits the NuGet packages for
known vulnerabilities, fails on model changes without a migration and verifies the formatting.

| Folder | Contents |
| --- | --- |
| `ManageGames/Controllers` | Home (public pages), Account, Games, Consoles, Companies, Users |
| `ManageGames/Services` | Data access per area, on EF Core |
| `ManageGames/Auth` | Claims, server-side cookie validation, forced password change |
| `ManageGames/Data`, `ManageGames/Migrations` | EF Core model and migrations |
| `ManageGames.Tests` | xUnit integration tests |
| `docs` | Landing page, published with GitHub Pages |

## Ideas

- Mobile-friendly layout and a dark mode
- Notes, cover pictures and photos of your own copies (to document their condition) per game
- Per-game flags such as "has its case" or "backup made"
- Wishlist priorities
- Tracking owned consoles and handhelds like games
- A searchable console dropdown
- An admin view of all users' games
- A settings page
