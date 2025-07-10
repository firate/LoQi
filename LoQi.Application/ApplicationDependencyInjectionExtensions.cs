using LoQi.Application.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoQi.Application;

public static class ApplicationDependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ILogService, LogService>();
        
        return services;
    }
}

