using System.Security.Claims;
using System.Threading.RateLimiting;
using ManageGames.Auth;
using ManageGames.Data;
using ManageGames.Models;
using ManageGames.Services;
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

// The keys that encrypt the cookies. By default they live in the user profile, which is fine on one
// machine; in a container, point DataProtection:KeysPath to a persistent volume, otherwise every
// restart signs everybody out.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("ManageGames");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

// SQLite database under the content root unless a connection string is configured. The setting is
// read when a context is created, so configuration overrides (e.g. from the tests) are honored.
var defaultDbPath = Path.Combine(builder.Environment.ContentRootPath, "DB", "DataBase.db");
Directory.CreateDirectory(Path.GetDirectoryName(defaultDbPath)!);
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("ManageGames")
        ?? $"Data Source={defaultDbPath}"));

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<ConsoleService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuthCookieEvents>();

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

// Apply pending migrations (creates the database on first run), hash passwords that old versions
// stored in plaintext and create the admin role and the first admin.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    var users = scope.ServiceProvider.GetRequiredService<UserService>();
    await users.HashPlaintextPasswordsAsync();
    await users.EnsureInitialAdminAsync(app.Configuration["Seed:AdminUsername"], app.Configuration["Seed:AdminPassword"]);
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
