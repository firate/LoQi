using System.Data;
using LoQi.Application.Persistence;

namespace LoQi.Persistence;

public class DataContext
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DataContext(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IDbConnection CreateConnection()
    {
        return _connectionFactory.GetConnection();
    }
}