using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace LoQi.Persistence;

public class DatabaseInitializer(IConfiguration configuration, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        logger.LogInformation("Initializing database schema...");

        var sql = ReadSchemaFile();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();

        logger.LogInformation("Database schema initialized successfully");
    }

    private static string ReadSchemaFile()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("schema-creation.sql");

        if (stream is null)
            throw new FileNotFoundException("schema-creation.sql embedded resource not found");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}