using System.Data;
using LoQi.Application.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace LoQi.Persistence;

public class SqliteConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public IDbConnection GetConnection()
    {
        return new SqliteConnection(configuration.GetConnectionString("DefaultConnection"));
    }
}