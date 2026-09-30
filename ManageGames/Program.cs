using System.Security.Claims;
using System.Threading.RateLimiting;
using ManageGames.Auth;
using ManageGames.Data;
using ManageGames.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST must carry the antiforgery token the form tag helper emits (CSRF protection).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<RequirePasswordChangeFilter>();
});

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
builder.Services.AddScoped<AuthCookieEvents>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // A distinct name, because cookies on localhost are shared by all ports, i.e. by every local app.
        // The cookie is HttpOnly by default and marked Secure whenever the request is HTTPS.
        options.Cookie.Name = "ManageGames.Auth";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        // The start page hosts the login form and opens it when a protected page redirects there.
        options.LoginPath = "/";
        options.AccessDeniedPath = "/Account/AccessDenied";
        // Checks each request's cookie against the user's security stamp (server-side revocation).
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
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        return new ValueTask(context.HttpContext.Response.WriteAsync(
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

var app = builder.Build();

// Apply pending migrations (creates the database on first run), hash passwords that old versions
// stored in plaintext and create the first admin.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    var users = scope.ServiceProvider.GetRequiredService<UserService>();
    users.HashPlaintextPasswords();
    users.EnsureInitialAdmin(app.Configuration["Seed:AdminUsername"], app.Configuration["Seed:AdminPassword"]);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

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

app.Run();
