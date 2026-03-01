using System.Data;
using LoQi.Application.Persistence;

namespace LoQi.Persistence;

public class DataContext(IDbConnectionFactory connectionFactory)
{
    public IDbConnection CreateConnection()
    {
        return connectionFactory.GetConnection();
    }
}