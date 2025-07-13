using System.Data;
using LoQi.Application.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace LoQi.Persistence;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IDbConnection GetConnection()
    {
        return new SqliteConnection(_configuration.GetConnectionString("DefaultConnection"));
    }
}