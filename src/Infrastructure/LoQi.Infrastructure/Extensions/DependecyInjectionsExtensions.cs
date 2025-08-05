using LoQi.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LoQi.Infrastructure.Extensions;

public static class DependecyInjectionsExtensions
{
   // <summary>
    /// Add Redis Stream services to dependency injection container
    /// </summary>
    public static IServiceCollection AddRedisStream(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure Redis Stream settings
        services.Configure<RedisStreamConfig>(
            configuration.GetSection("LoQi:Redis:Stream"));

        // Configure Redis connection
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
        
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            options.AsyncTimeout = 5000;
        
            return ConnectionMultiplexer.Connect(options);
        });

        // AddScoped yerine AddSingleton kullan
        services.AddSingleton<IRedisStreamService, RedisStreamService>();

        return services;
    }
}