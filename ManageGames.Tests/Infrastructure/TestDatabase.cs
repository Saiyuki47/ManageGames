using Npgsql;
using Testcontainers.PostgreSql;

namespace ManageGames.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL container for the whole test run (started on first use; Testcontainers removes it when
/// the run ends), with a separate, throw-away database per test class. Needs Docker.
/// </summary>
public static class TestDatabase
{
    // The same major version as compose.yaml.
    public const string Image = "postgres:18-alpine";

    private static readonly Lazy<Task<PostgreSqlContainer>> Server = new(StartAsync);

    /// <summary>Creates a new, empty database and returns its connection string.</summary>
    public static async Task<string> CreateAsync()
    {
        var server = await Server.Value;
        var database = $"managegames_{Guid.NewGuid():N}";
        await ExecuteOnServerAsync(server.GetConnectionString(), $"""CREATE DATABASE "{database}" """);
        return new NpgsqlConnectionStringBuilder(server.GetConnectionString()) { Database = database }.ConnectionString;
    }

    public static async Task DropAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database!;
        await using (var pooled = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(pooled);
        }

        await ExecuteOnServerAsync(connectionString, $"""DROP DATABASE IF EXISTS "{database}" WITH (FORCE)""");
    }

    // Runs a statement in the server's maintenance database. The database names in the statements are
    // generated from GUIDs above, so they are safe to put into the SQL.
    private static async Task ExecuteOnServerAsync(string connectionString, string sql)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var container = new PostgreSqlBuilder(Image).Build();
        await container.StartAsync();
        return container;
    }
}
