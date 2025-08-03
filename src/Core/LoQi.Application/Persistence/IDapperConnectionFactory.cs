using System.Data;

namespace LoQi.Application.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection GetConnection();
}