using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ManageGames.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ManageGames.Tests;

public class HostingTests(ManageGamesFactory factory) : IClassFixture<ManageGamesFactory>
{
    [Fact]
    public void Tests_UseTheirOwnDatabase_NotTheLocalDevelopmentOne()
    {
        var database = factory.Query(db => db.Database.GetDbConnection().Database);

        Assert.Equal(new NpgsqlConnectionStringBuilder(factory.ConnectionString).Database, database);
        Assert.StartsWith("managegames_", database, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthEndpoint_ReportsHealthy_WithoutSigningIn()
    {
        var response = await factory.CreateBrowser().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>Uses the app's own key storage instead of the tests' in-memory keys.</summary>
public class DatabaseKeysFactory : ManageGamesFactory
{
    protected override bool UseEphemeralKeys => false;
}

public class DataProtectionTests(DatabaseKeysFactory factory) : IClassFixture<DatabaseKeysFactory>
{
    [Fact]
    public async Task CookieKeys_AreStoredInTheDatabase_SoAllInstancesShareThem()
    {
        await factory.SignInAsAdminAsync();

        Assert.True(factory.Query(db => db.DataProtectionKeys.Any()));
    }
}

/// <summary>Database key storage with the keys encrypted by a (self-signed, test-only) certificate.</summary>
public sealed class CertificateKeysFactory : DatabaseKeysFactory
{
    private const string CertificatePassword = "test-only-certificate";
    private readonly string _certificatePath = Path.Combine(Path.GetTempPath(), $"managegames-keys-{Guid.NewGuid():N}.pfx");

    public CertificateKeysFactory()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=ManageGames test keys", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(_certificatePath, certificate.Export(X509ContentType.Pfx, CertificatePassword));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_certificatePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // Host settings: Program.cs reads these while it registers the services.
        builder.UseSetting("DataProtection:CertificatePath", _certificatePath);
        builder.UseSetting("DataProtection:CertificatePassword", CertificatePassword);
    }
}

public class CertificateProtectedKeysTests(CertificateKeysFactory factory) : IClassFixture<CertificateKeysFactory>
{
    [Fact]
    public async Task CookieKeys_AreEncryptedWithTheConfiguredCertificate()
    {
        var browser = await factory.SignInAsAdminAsync();

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Games")).StatusCode);
        var keys = factory.Query(db => db.DataProtectionKeys.Select(k => k.Xml).ToList());
        Assert.NotEmpty(keys);
        Assert.All(keys, xml => Assert.Contains("EncryptedXmlDecryptor", xml, StringComparison.Ordinal));
    }
}
