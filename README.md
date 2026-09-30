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

Updating from an older version migrates the database automatically at the next start: accounts
move to ASP.NET Core Identity, and passwords that don't meet the current rules (15 characters and
more) still work once, but have to be replaced right after logging in.

### Configuration

Settings can come from `appsettings.json`, user secrets or environment variables
(for example `Seed__AdminPassword`).

| Setting | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings:ManageGames` | SQLite connection string | `ManageGames/DB/DataBase.db` |
| `Seed:AdminUsername` | Name of the first admin, used only on an empty database | `admin` |
| `Seed:AdminPassword` | Password of the first admin (no forced change; must meet the password rules) | a generated one-time password |
| `RateLimiting:LoginPermitLimit` | Login attempts per minute and client IP | `10` |
| `RateLimiting:PasswordChangePermitLimit` | Password change attempts per minute and user | `10` |
| `Authentication:AbsoluteSessionLifetime` | Maximum length of a session, however active | `12:00:00` |
| `DataProtection:KeysPath` | Folder for the keys that encrypt the cookies (set it in containers) | the user profile |
| `AllowedHosts` | Host names the app answers to; set it to your domain when it is reachable from outside | `*` |

### Running it for others

- Serve it over HTTPS only: all cookies use the `__Host-` prefix, which browsers accept over HTTPS only.
- Behind a reverse proxy, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, so the app sees the
  clients' addresses (for the rate limits) and the original scheme. Only do this when the app can't
  be reached directly, since it trusts the forwarded headers.
- In a container, point `DataProtection:KeysPath` to a persistent volume, otherwise every restart
  signs everybody out.

## Security

- Accounts are managed by ASP.NET Core Identity. Passwords are stored as salted PBKDF2-HMAC-SHA512
  hashes with 210,000 iterations (OWASP); older hashes are upgraded at the next login.
- Password rules follow NIST SP 800-63B-4: at least 15 characters and no composition rules, but a
  blocklist of guessable passwords (repetitions, sequences like `12345` or `qwertz`, common words,
  the username). Generated, admin-assigned and reset passwords have to be replaced at the next login.
- Logins are rate-limited per client IP, and five wrong passwords lock the account for five minutes.
  Failures always show the same message and take the same time, so they don't reveal which accounts
  exist. Password changes are rate-limited per user.
- The auth cookie is a `__Host-` cookie: HttpOnly, Secure, `SameSite=Strict`, gone when the browser
  closes. It carries a server-side session id, checked on every request: logging out ends this
  device's session (and makes every copy of its cookie worthless), while other devices stay signed
  in. Changing or resetting a password ends all sessions. Sessions end after two hours without
  activity and after twelve hours at the latest.
- Games are scoped to their owner in every query; consoles, companies and users require the admin role.
- All forms are protected against CSRF. Responses carry a strict Content Security Policy and the
  usual security headers, and HSTS is sent outside of development.
- Logins, lockouts, rate limit hits, password changes and account changes are logged.

## Development

```bash
dotnet test                        # integration tests: in-memory host, throw-away SQLite databases
dotnet format ManageGames.sln      # code style from .editorconfig (verified in CI)
dotnet tool restore                # once, for the pinned dotnet-ef and libman
dotnet dotnet-ef migrations add <Name> --project ManageGames   # after changing the model
```

The build treats warnings as errors and runs the .NET code analyzers (`latest-recommended`) and the
code style checks. The CI workflow additionally runs the tests, audits the NuGet packages for known
vulnerabilities, fails on model changes without a migration and verifies the formatting. Dependabot
proposes updates for the NuGet packages, the .NET tools and the GitHub Actions every week.

Bootstrap is managed with LibMan (`ManageGames/libman.json`), which Dependabot doesn't cover. To
update it, change the version there and run `dotnet libman restore` in `ManageGames`.

| Folder | Contents |
| --- | --- |
| `ManageGames/Controllers` | Home (public pages), Account, Games, Consoles, Companies, Users |
| `ManageGames/Services` | Data access per area on EF Core, accounts and sessions on ASP.NET Core Identity |
| `ManageGames/Auth` | Password rules, sessions in the auth cookie, security headers and logging, forced password change |
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
