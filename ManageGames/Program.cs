using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using ManageGames.Auth;
using ManageGames.Data;
using ManageGames.Models;
using ManageGames.Services;
using ManageGames.Services.Covers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Don't announce the server software in every response.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST must carry the antiforgery token the form tag helper emits (CSRF protection).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<RequirePasswordChangeFilter>();
});

// All cookies use the __Host- prefix: browsers then only accept them over HTTPS, for the whole site and
// for this exact host, so neither plain HTTP nor a subdomain can set or overwrite them.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-ManageGames.Antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.Configure<CookieTempDataProviderOptions>(options =>
{
    options.Cookie.Name = "__Host-ManageGames.TempData";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// The keys that encrypt the cookies live in the database, so every instance of the app shares them
// and restarts or new containers don't sign anybody out. Outside development, encrypt them with a
// certificate (DataProtection:CertificatePath), so a copy of the database alone can't forge cookies.
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("ManageGames")
    .PersistKeysToDbContext<AppDbContext>();
if (builder.Configuration["DataProtection:CertificatePath"] is { Length: > 0 } certificatePath)
{
    dataProtection.ProtectKeysWithCertificate(
        X509CertificateLoader.LoadPkcs12FromFile(certificatePath, builder.Configuration["DataProtection:CertificatePassword"]));
}

// PostgreSQL. The connection string is read when a context is created, so configuration overrides
// (e.g. from the tests) are honored. Transient connection failures are retried.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseNpgsql(
        services.GetRequiredService<IConfiguration>().GetConnectionString("ManageGames")
            ?? throw new InvalidOperationException(
                "The PostgreSQL connection string ConnectionStrings:ManageGames is not configured (see the README)."),
        npgsql => npgsql.EnableRetryOnFailure()));

// For load balancers and container platforms: /healthz answers "Healthy" while the database is reachable.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<ConsoleService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuthCookieEvents>();

// Cover images from IGDB and SteamGridDB, for the sources whose API keys are configured (see the README).
builder.Services.AddCoverSearch(builder.Configuration);

builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
    {
        // NIST SP 800-63B-4: a minimum length and a blocklist (CommonPasswordValidator) instead of
        // composition rules like "one digit and one symbol".
        options.Password.RequiredLength = PasswordPolicy.MinLength;
        options.Password.RequiredUniqueChars = PasswordPolicy.MinUniqueChars;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        // Usernames may contain any characters, as before; UsernameNormalizer compares them.
        options.User.AllowedUserNameCharacters = string.Empty;
        // Per-account throttling on top of the per-IP rate limit: wrong passwords from many IPs still
        // lock the account for a while.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
    .AddPasswordValidator<CommonPasswordValidator>();
builder.Services.Replace(ServiceDescriptor.Scoped<ILookupNormalizer, UsernameNormalizer>());
// Existing hashes with fewer iterations are upgraded automatically at the user's next login.
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = PasswordPolicy.HashIterationCount);
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Identity re-reads the user's claims periodically; this browser's session id has to survive that.
    options.OnRefreshingPrincipal = context =>
    {
        if (context.CurrentPrincipal?.GetSessionId() is { } sessionId && context.NewPrincipal?.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(UserClaims.SessionIdType, sessionId));
        }
        return Task.CompletedTask;
    };
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-ManageGames.Auth";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    // Ends after two hours without activity; SessionService adds an absolute limit on top.
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    // The start page hosts the login form and opens it when a protected page redirects there.
    options.LoginPath = "/";
    options.AccessDeniedPath = "/Account/AccessDenied";
    // Server-side sessions, revocation and Identity's security stamp check.
    options.EventsType = typeof(AuthCookieEvents);
});

builder.Services.AddAuthorization(options =>
{
    // Secure by default: every endpoint requires a signed-in user unless it is marked [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ManageGames.RateLimiting")
            .RateLimited(httpContext.Request.Path.ToString(), httpContext.Connection.RemoteIpAddress?.ToString());
        httpContext.Response.ContentType = "text/plain; charset=utf-8";
        return new ValueTask(httpContext.Response.WriteAsync(
            "Too many attempts. Please wait a minute and try again.", cancellationToken));
    };
    options.AddPolicy(RateLimitPolicies.Login, httpContext =>
    {
        var permitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:LoginPermitLimit", 10);
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
    });
    // Per user, so a hijacked session can't guess the current password to take over the account.
    options.AddPolicy(RateLimitPolicies.PasswordChange, httpContext =>
    {
        var permitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("RateLimiting:PasswordChangePermitLimit", 10);
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
    });
});

// One year, as recommended once HTTPS works reliably (only sent outside of Development and localhost).
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

var app = builder.Build();

// Apply pending migrations: on the first run they create the database, the tables and the starting
// data (consoles and their makers). EF Core locks the database meanwhile, so several instances can
// start at once. Then create the admin role and, on an empty database, the first admin.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    var users = scope.ServiceProvider.GetRequiredService<UserService>();
    await users.EnsureInitialAdminAsync(app.Configuration["Seed:AdminUsername"], app.Configuration["Seed:AdminPassword"]);
    var covers = scope.ServiceProvider.GetRequiredService<CoverService>();
    if (await covers.CreateMissingThumbnailsAsync() is > 0 and var thumbnails)
    {
        app.Logger.ThumbnailsCreated(thumbnails);
    }

    // `dotnet ManageGames.dll reset-password <username>`: prints a one-time password for the account and
    // exits, for when nobody can log in anymore.
    if (args is ["reset-password", var username])
    {
        Environment.ExitCode = await users.ResetToOneTimePasswordAsync(username) == null ? 1 : 0;
        return;
    }

    // `dotnet ManageGames.dll scrape-covers [--refresh] [username]`: searches covers for all games (of one user)
    // that have none yet, e.g. the ones added before the cover search existed, and exits. --refresh also swaps
    // automatically found covers for ones of the preferred regions (Covers:Regions) where a source has them.
    if (args is ["scrape-covers", .. var scrapeArgs])
    {
        var refresh = scrapeArgs.Contains("--refresh", StringComparer.Ordinal);
        var usernames = scrapeArgs.Where(a => a != "--refresh").ToList();
        Environment.ExitCode = usernames.Count <= 1
            && await covers.FindMissingCoversAsync(usernames.SingleOrDefault(), refresh, TimeSpan.FromMilliseconds(300)) ? 0 : 1;
        return;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// After the exception handler, so the error page gets the headers too.
app.UseSecurityHeaders();
app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
// After authentication, so the password-change policy can partition by the signed-in user.
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
