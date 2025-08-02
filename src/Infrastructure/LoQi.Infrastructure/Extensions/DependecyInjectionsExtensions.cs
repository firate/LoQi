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
        // Configure Redis Stream settings - İki yöntemden birini kullan:
        
        // Yöntem 1: Direct configuration binding (Önerilen)
        services.Configure<RedisStreamConfig>(
            configuration.GetSection("LoQi:Redis:Stream"));

        // Yöntem 2: Manual binding (eğer yukarısı çalışmazsa)
        // services.Configure<RedisStreamConfig>(options =>
        //     configuration.GetSection("LoQi:Redis:Stream").Bind(options));

        // Configure Redis connection
        var redisConnectionString = configuration.GetConnectionString("Redis") 
                                    ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            
            // Production optimizations
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            options.AsyncTimeout = 5000;
            
            return ConnectionMultiplexer.Connect(options);
        });

        // Register Redis Stream service
        services.AddScoped<IRedisStreamService, RedisStreamService>();

        return services;
    }

    /// <summary>
    /// Initialize Redis Stream consumer groups at startup
    /// </summary>
    public static async Task InitializeRedisStreamAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var redisStreamService = scope.ServiceProvider.GetRequiredService<IRedisStreamService>();

        // Create default consumer groups
        var consumerGroups = new[]
        {
            "processed-logs",   // Successfully parsed logs → SQLite
            "failed-logs",      // Parse failures → Error handling
            "retry-logs"        // Retryable errors → Retry logic
        };

        foreach (var group in consumerGroups)
        {
            await redisStreamService.CreateConsumerGroupAsync(group);
        }
    }
}