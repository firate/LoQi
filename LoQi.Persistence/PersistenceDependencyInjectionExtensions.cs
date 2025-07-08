using LoQi.Application.Persistence;
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

        return services;
    }
}