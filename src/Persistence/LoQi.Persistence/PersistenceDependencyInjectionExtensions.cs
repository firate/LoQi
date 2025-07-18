using LoQi.Application.Persistence;
using LoQi.Application.Repository;
using LoQi.Persistence.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoQi.Persistence;

public static class PersistenceDependencyInjectionExtensions
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddScoped<DataContext>();
        services.AddScoped<ILogRepository, LogRepository>();

        return services;
    }
}

