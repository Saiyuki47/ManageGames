# ManageGames

A self-hosted web app to keep track of your video game collection: your games with their cover
pictures, the consoles they run on, the companies that make those consoles, and a wishlist. Several
people can use one installation; everybody sees only their own collection.

Built with ASP.NET Core MVC on .NET 10, Entity Framework Core and PostgreSQL, with accounts managed by
ASP.NET Core Identity. Covers come from ScreenScraper, IGDB and SteamGridDB and are scaled down with
SkiaSharp.

## Features

- **Your games**: add, edit and delete games, with their console and the number of copies you own.
- **Cover pictures**: adding a game looks up its cover at [ScreenScraper](https://www.screenscraper.fr),
  [IGDB](https://www.igdb.com) and [SteamGridDB](https://www.steamgriddb.com), preferring German and
  European boxes, and shows it in the lists. If the automatic pick is wrong or missing, choose another
  one from all results of all sources, or upload your own picture.
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

The app works without any further setup. To get cover pictures automatically, add the free credentials
of at least one cover source, see [Cover pictures](#cover-pictures).

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

### Cover pictures

Covers come from up to three sources, tried in this order:

1. **[ScreenScraper](https://www.screenscraper.fr)**: scans of the real boxes of each region, so German
   games get their German box, and the regional titles, so German titles are found. Needs developer
   credentials (see below).
2. **[IGDB](https://www.igdb.com)**: knows the platforms of each game, so it picks the cover for the
   game's console; mostly North American covers, a few European ones.
3. **[SteamGridDB](https://www.steamgriddb.com)**: community-made covers, for games the others don't know.

Sources without credentials are skipped, so one is enough; without any, everything works except the
search, and covers can still be uploaded. Among equally fitting covers of a source, the preferred regions
win (`Covers:Regions`, German, then European by default).

- **ScreenScraper**: register at [screenscraper.fr](https://www.screenscraper.fr), then ask for developer
  credentials (devid and devpassword) in the ScreenScraper forum; they are free for non-commercial
  software. Your ScreenScraper login alone isn't enough: apps like ES-DE or Skraper only ask for it because
  they ship their own developer credentials. Optionally add your login as well, which raises the number of
  requests you may make per day.
- **IGDB**: log in to the [Twitch developer console](https://dev.twitch.tv/console) (Twitch account
  with two-factor authentication), register an application (OAuth redirect URL `http://localhost`,
  client type *Confidential*), then create a client secret. IGDB is free for non-commercial use under the
  [Twitch Developer Services Agreement](https://www.twitch.tv/p/legal/developer-agreement/).
- **SteamGridDB**: log in at [steamgriddb.com](https://www.steamgriddb.com) and create a key under
  [Preferences → API](https://www.steamgriddb.com/profile/preferences/api).

Keep the credentials out of the repository. For development, store them as user secrets, one command per
value (leave out the sources you don't use):

```bash
dotnet user-secrets set "Covers:ScreenScraper:DevId" "<devid>" --project ManageGames
```

```bash
dotnet user-secrets set "Covers:ScreenScraper:DevPassword" "<devpassword>" --project ManageGames
```

```bash
dotnet user-secrets set "Covers:ScreenScraper:Username" "<your ScreenScraper login>" --project ManageGames
```

```bash
dotnet user-secrets set "Covers:ScreenScraper:Password" "<your ScreenScraper password>" --project ManageGames
```

```bash
dotnet user-secrets set "Covers:Igdb:ClientId" "<client id>" --project ManageGames
```

```bash
dotnet user-secrets set "Covers:Igdb:ClientSecret" "<client secret>" --project ManageGames
```

```bash
dotnet user-secrets set "Covers:SteamGridDb:ApiKey" "<api key>" --project ManageGames
```

To check what is stored (this prints the values too, so not while sharing your screen):

```bash
dotnet user-secrets list --project ManageGames
```

Restart the app afterwards; the sources with credentials are used from then on. On a server, use
environment variables instead, e.g. `Covers__ScreenScraper__DevId` or `Covers__Igdb__ClientSecret`.

- **Adding a game** waits up to 15 seconds for its cover. It only takes a cover whose title clearly
  matches and that isn't for another console; a wrong cover is worse than none.
- **Editing a game** shows its cover. *Find a cover* lists the results of all sources, the ones for
  the game's console and the preferred regions first, with the region of each box, and lets you change the
  search (e.g. to the English title); clicking one stores it. You can also upload a JPEG, PNG, WebP or GIF
  of up to 5 MB, or remove the cover. Changing a game's title or console searches again if it has no
  cover yet.
- **The lists** show small versions of the covers (a few kilobytes each), made when a cover is stored;
  covers stored before that get theirs at the next start.
- **Games added earlier** get their covers with one command; it searches every game without a cover, of
  all users or of one:

```bash
dotnet run --project ManageGames -- scrape-covers [username]
```

- **Better regional covers later**, e.g. after setting up ScreenScraper or changing `Covers:Regions`:
  `--refresh` also looks again for the covers the automatic search picked earlier and replaces them where
  a source has one of a preferred region. Covers you chose or uploaded are never replaced.

```bash
dotnet run --project ManageGames -- scrape-covers --refresh [username]
```

A published build runs it as `dotnet ManageGames.dll scrape-covers [--refresh] [username]`.

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
| `Covers:ScreenScraper:DevId`, `Covers:ScreenScraper:DevPassword` | ScreenScraper developer credentials, see [Cover pictures](#cover-pictures) | none: ScreenScraper isn't used |
| `Covers:ScreenScraper:Username`, `Covers:ScreenScraper:Password` | Your ScreenScraper login, for more requests per day | none |
| `Covers:Igdb:ClientId`, `Covers:Igdb:ClientSecret` | IGDB access (a Twitch application) | none: IGDB isn't used |
| `Covers:SteamGridDb:ApiKey` | SteamGridDB API key | none: SteamGridDB isn't used |
| `Covers:Providers` | The cover sources in the order the automatic search tries them | `ScreenScraper,IGDB,SteamGridDB` |
| `Covers:Regions` | Preferred box art regions, best first (ScreenScraper's short names: `de`, `eu`, `fr`, `uk`, `us`, `jp`, `wor`, ...) | `de,eu` |
| `Covers:AutoSearchTimeout` | How long adding a game waits for its cover | `00:00:15` |
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
- **Cover sources** (optional): `Covers__ScreenScraper__DevId`, `Covers__ScreenScraper__DevPassword`,
  `Covers__Igdb__ClientId`, `Covers__Igdb__ClientSecret`, `Covers__SteamGridDb__ApiKey`, and so on (see
  [Configuration](#configuration)). The server needs outgoing HTTPS to the sources; the image library's
  native parts for Linux come with the app, nothing else has to be installed.

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
- **Covers**: images are only downloaded from the image servers of the configured sources (HTTPS, no
  redirects followed), at most 5 MB, and stored only if their content really is a JPEG, PNG, WebP or
  GIF, whatever the file name or the server claims; uploads are checked the same way. The browser
  loads every picture from the app itself, including the previews of search results, so the Content
  Security Policy stays strict and the sources never see who looks at them. Credentials that a source
  wants in its addresses (ScreenScraper) are added only to the app's own requests; they never appear in
  pages or forms, and request logs leave out the query string.
- **Logging**: logins, lockouts, rate limit hits, password changes and resets and account changes are
  logged, never with a password (except the one-time passwords, which are only good for one login).

## How the data is stored

| Tables | Contents |
| --- | --- |
| `Games` | The games of every user, with console, copies and the wishlist flag |
| `GameCovers` | One cover image per game with a small version for the lists, its source and whether the automatic search picked it; deleted together with the game |
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

- **Tests** are mostly integration tests: they host the real app in memory and send requests like a
  browser, against real PostgreSQL. Testcontainers starts a throw-away PostgreSQL container for the run,
  with a separate database per test class, and removes it afterwards. The cover sources are tested with
  recorded answers of their APIs and a fake source, so the tests never reach the internet and need no
  credentials. They use xUnit v3 on Microsoft.Testing.Platform (enabled in [global.json](global.json)).
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
| `ManageGames/Program.cs` | Startup: services, security settings, migrations, the request pipeline, the `reset-password` and `scrape-covers` commands |
| `ManageGames/Controllers` | Home (public pages), Account, Games, Covers, Consoles, Companies, Users |
| `ManageGames/Services` | Games, covers, consoles and companies on EF Core; accounts, sessions and user administration on Identity |
| `ManageGames/Services/Covers` | The cover search: the ScreenScraper, IGDB and SteamGridDB sources, title, console and region matching, image downloads, thumbnails |
| `ManageGames/Auth` | Password rules, session checks for the auth cookie, security headers, security logging, forced password change |
| `ManageGames/Data`, `ManageGames/Models` | The EF Core database context and the entities |
| `ManageGames/Migrations` | The database schema and the console catalog |
| `ManageGames/ViewModels`, `ManageGames/Views` | Forms with their validation rules, and the Razor pages |
| `ManageGames/wwwroot` | CSS, JavaScript and Bootstrap |
| `ManageGames.Tests` | Integration and unit tests; `Infrastructure` holds the test host, the test databases, browser helpers and a fake cover source |
| `compose.yaml` | The local PostgreSQL for development |
| `docs` | Landing page, published with GitHub Pages |

## Ideas

- Mobile-friendly layout and a dark mode
- More cover sources (TheGamesDB, RAWG)
- Notes and photos of your own copies (to document their condition) per game
- Per-game flags such as "has its case" or "backup made"
- Wishlist priorities
- Tracking owned consoles and handhelds like games
- A searchable console dropdown
- An admin view of all users' games
- A settings page
