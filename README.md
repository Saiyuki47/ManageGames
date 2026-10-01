# ManageGames

A self-hosted web app to keep track of your video game collection: your games, the consoles they run
on, the companies that make those consoles, and a wishlist. Several people can use one installation;
everybody sees only their own collection.

Built with ASP.NET Core MVC on .NET 10, Entity Framework Core and PostgreSQL, with accounts managed by
ASP.NET Core Identity.

## Features

- **Your games**: add, edit and delete games, with their console and the number of copies you own.
- **Wishlist**: the games you still want, kept apart from your collection.
- **Search and sorting**: lists can be searched (ignoring case and spaces) and sort alphabetically,
  with umlauts next to their base letter.
- **Consoles and companies**: a shared catalog for everybody, managed by admins. A new installation
  already contains 63 well-known consoles of 23 makers, from the Magnavox Odyssey to the Nintendo
  Switch 2, each linked to its maker.
- **User administration** for admins: create accounts (as users or admins), reset forgotten
  passwords, delete accounts together with their games.
- **Your account**: change your password; logging out ends the session on this device only.

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[Docker](https://www.docker.com/products/docker-desktop/), which runs the PostgreSQL database for
development and the tests.

```bash
git clone https://github.com/Saiyuki47/ManageGames.git
cd ManageGames
docker compose up -d
dotnet run --project ManageGames
```

Then open <https://localhost:7200>. In Visual Studio or Rider, start the `ManageGames` launch profile
instead of `dotnet run`.

`docker compose up -d` is only needed once: the database container then starts together with Docker.
If Docker isn't running, the app stops right at startup with "Failed to connect to 127.0.0.1:5433".

### What the first start does

1. **Docker** creates an empty PostgreSQL 18 database `managegames` (user `managegames`) as defined in
   [compose.yaml](compose.yaml). It listens on `127.0.0.1:5433` only, so it can't clash with a
   PostgreSQL installed directly on the machine and isn't reachable from the network. Its data lives in
   the Docker volume `<folder>_postgres-data` and survives restarts and `docker compose down`;
   `docker compose down -v` deletes it.
2. **The app** applies its migrations: it creates the tables (and the database itself, if it doesn't
   exist yet) and fills in the catalog of consoles and companies. This happens once per database;
   admins can change or delete the entries afterwards.
3. **The app** creates the admin role and the account `admin` with a random one-time password, which it
   prints to the console. Log in with it; you then have to choose your own password.

### Forgot your password?

Admins reset other users' passwords under **Users**. If nobody can log in anymore, give an account a
new one-time password from the command line. It is printed to the console, and the user chooses their
own password at the next login. This also lifts a lockout and signs the account out everywhere.

```bash
dotnet run --project ManageGames -- reset-password <username>
```

A published build runs the same command as `dotnet ManageGames.dll reset-password <username>`.

## Configuration

Settings come from `appsettings.json`, `appsettings.Development.json`, user secrets or environment
variables (write `__` instead of `:`, e.g. `ConnectionStrings__ManageGames`).

| Setting | Purpose | Default |
| --- | --- | --- |
| `ConnectionStrings:ManageGames` | PostgreSQL connection string, e.g. `Host=db;Database=managegames;Username=managegames;Password=…` | the `compose.yaml` database in Development; required otherwise |
| `Seed:AdminUsername` | Name of the first admin, created on an empty database | `admin` |
| `Seed:AdminPassword` | Password of the first admin instead of a generated one; it must meet the password rules and isn't printed or forced to change | a generated one-time password |
| `RateLimiting:LoginPermitLimit` | Login attempts per minute and client IP | `10` |
| `RateLimiting:PasswordChangePermitLimit` | Password change attempts per minute and user | `10` |
| `Authentication:AbsoluteSessionLifetime` | Maximum length of a session, however active | `12:00:00` |
| `DataProtection:CertificatePath` | PFX certificate that encrypts the cookie keys stored in the database | not encrypted (the app logs a warning) |
| `DataProtection:CertificatePassword` | Password of that PFX file | none |
| `AllowedHosts` | Host names the app answers to; set it to your domain when others can reach it | `*` |

The development connection string, including the password of the local container, is in
[appsettings.Development.json](ManageGames/appsettings.Development.json); it is meant for that container
only.

## Running it for others

The app is a single ASP.NET Core application that renders its pages on the server; it needs a
PostgreSQL database and HTTPS in front of it.

```bash
dotnet publish ManageGames -c Release -o publish
```

Run `publish/ManageGames.dll` with `dotnet`, configured through environment variables:

- **Database**: `ConnectionStrings__ManageGames` pointing to a PostgreSQL you back up, e.g. a managed
  one (Azure, AWS, Google Cloud, Neon, ...) or your own with regular `pg_dump` backups. Don't expose it
  to the internet; only the app needs to reach it. Keep the password in an environment variable or a
  secret store, not in `appsettings.json`.
- **HTTPS**: put a reverse proxy such as Caddy or nginx in front, which handles the certificate. Let the
  app listen locally only (`ASPNETCORE_URLS=http://127.0.0.1:8080`) and set
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, so it sees the clients' addresses and that they use
  HTTPS. HTTPS is required: browsers accept the app's `__Host-` cookies over HTTPS only.
- **Cookie keys**: set `DataProtection__CertificatePath` (and `__CertificatePassword`) so the keys
  that encrypt the cookies are stored encrypted in the database.
- **Host name**: set `AllowedHosts` to your domain.
- **First admin**: read the one-time password from the log of the first start, or set
  `Seed__AdminPassword` beforehand.

Several instances behind a load balancer work: sessions and cookie keys live in the database, and EF
Core locks the database while one instance applies migrations at startup. The login rate limits count
per instance; put a global limit into the reverse proxy if you need one. `/healthz` answers `Healthy`
while the database is reachable, for load balancers and container platforms.

## Security

- **Passwords**: stored by ASP.NET Core Identity as salted PBKDF2-HMAC-SHA512 hashes with 210,000
  iterations (OWASP). The rules follow NIST SP 800-63B-4: at least 15 characters and no composition
  rules, but a blocklist of guessable passwords (repetitions, sequences like `12345` or `qwertz`, common
  words, the username). Generated, admin-assigned and reset passwords have to be replaced at the next
  login.
- **Login**: rate-limited per client IP; five wrong passwords lock the account for five minutes.
  Failures always show the same message and take the same time, so they don't reveal which accounts
  exist. Password changes are rate-limited per user.
- **Sessions**: the auth cookie is a `__Host-` cookie (HttpOnly, Secure, `SameSite=Strict`, gone when
  the browser closes) that carries a server-side session id, checked on every request. Logging out
  ends this device's session and makes every copy of its cookie worthless; changing or resetting a
  password ends all sessions. Sessions end after two hours without activity and after twelve hours at
  the latest.
- **Access**: every page requires a signed-in user unless it is public on purpose; games are scoped to
  their owner in every query; consoles, companies and users require the admin role. Admins can't delete
  their own account, so one admin always remains.
- **Web**: all forms are protected against CSRF; responses carry a strict Content Security Policy,
  `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` and `Permissions-Policy`, but no
  `Server` header; HSTS is sent outside of development.
- **Logging**: logins, lockouts, rate limit hits, password changes and resets and account changes are
  logged, never with a password (except the one-time passwords, which are only good for one login).

## How the data is stored

| Tables | Contents |
| --- | --- |
| `Games` | The games of every user, with console, copies and the wishlist flag |
| `Consoles`, `Companies` | The shared catalog; a console refers to its maker |
| `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, ... | Accounts and the Admin role (ASP.NET Core Identity) |
| `UserSessions` | One row per signed-in browser |
| `DataProtectionKeys` | The keys that encrypt the cookies, shared by all instances |
| `__EFMigrationsHistory` | The migrations applied so far |

Deleting a user deletes their games and sessions; deleting a console or company keeps the games and
consoles that refer to it, without a console or maker. All timestamps are stored in UTC.

## Development

```bash
dotnet test                        # all tests; needs Docker (see below)
dotnet test --coverage --coverage-output-format cobertura   # the same, with code coverage
dotnet format ManageGames.sln      # apply the code style from .editorconfig
dotnet tool restore                # once: the pinned dotnet-ef, libman and reportgenerator tools
dotnet dotnet-ef migrations add <Name> --project ManageGames   # after changing the model
```

- **Tests** are integration tests: they host the real app in memory and send requests like a browser,
  against real PostgreSQL. Testcontainers starts a throw-away PostgreSQL container for the run, with a
  separate database per test class, and removes it afterwards. They use xUnit v3 on
  Microsoft.Testing.Platform (enabled in [global.json](global.json)).
- **Code quality**: the build treats warnings as errors and runs the .NET code analyzers
  (`latest-recommended`), [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer)
  (async code, string comparisons, collections), [SonarAnalyzer](https://rules.sonarsource.com/csharp/)
  (bugs, code smells, security hotspots) and the code style rules of [.editorconfig](.editorconfig)
  (file-scoped namespaces, primary constructors, naming). Rules that don't fit the project are switched
  off in `.editorconfig` with the reason; deliberate exceptions in the code carry a `[SuppressMessage]`
  with a justification.
- **Coverage**: `dotnet test --coverage` measures it with Microsoft.Testing.Platform; for a browsable
  report run `dotnet reportgenerator -reports:TestResults/coverage.cobertura.xml -targetdir:TestResults/report
  -classfilters:-ManageGames.Migrations.*` and open `TestResults/report/index.html`.
- **Migrations**: the database schema comes from EF Core migrations in `ManageGames/Migrations`;
  starting data such as the console catalog is added by a migration too, so it is written once per
  database.
- **Bootstrap** is managed with LibMan ([ManageGames/libman.json](ManageGames/libman.json)). To update
  it, change the version there and run `dotnet libman restore` in `ManageGames`.

### Continuous integration

- [.github/workflows/dotnet.yml](.github/workflows/dotnet.yml) builds in Release (warnings and analyzer
  findings as errors), runs the tests with code coverage, fails on NuGet packages with known
  vulnerabilities and on model changes without a migration, and verifies the formatting. The coverage
  summary appears on the run's page, the HTML report is attached as the `coverage-report` artifact, and
  the job fails if coverage (without migrations) drops below 85 % of the lines or 70 % of the branches.
- The same workflow lints the workflows themselves with [actionlint](https://github.com/rhysd/actionlint)
  (syntax, expressions, shell scripts) and [zizmor](https://docs.zizmor.sh/) (security problems such as
  script injection or broad permissions). All actions are pinned to commit SHAs.
- [.github/workflows/pages.yml](.github/workflows/pages.yml) publishes the landing page in `docs` with
  GitHub Pages.
- [Dependabot](.github/dependabot.yml) proposes updates for the NuGet packages and analyzers, the .NET
  tools, the GitHub Actions (including their pinned SHAs) and the PostgreSQL image every week. It doesn't
  cover Bootstrap (LibMan) or the actionlint and zizmor versions in `dotnet.yml`.

### Project structure

| Path | Contents |
| --- | --- |
| `ManageGames/Program.cs` | Startup: services, security settings, migrations, the request pipeline, the `reset-password` command |
| `ManageGames/Controllers` | Home (public pages), Account, Games, Consoles, Companies, Users |
| `ManageGames/Services` | Games, consoles and companies on EF Core; accounts, sessions and user administration on Identity |
| `ManageGames/Auth` | Password rules, session checks for the auth cookie, security headers, security logging, forced password change |
| `ManageGames/Data`, `ManageGames/Models` | The EF Core database context and the entities |
| `ManageGames/Migrations` | The database schema and the console catalog |
| `ManageGames/ViewModels`, `ManageGames/Views` | Forms with their validation rules, and the Razor pages |
| `ManageGames/wwwroot` | CSS, JavaScript and Bootstrap |
| `ManageGames.Tests` | Integration tests; `Infrastructure` holds the test host, the test databases and browser helpers |
| `compose.yaml` | The local PostgreSQL for development |
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
