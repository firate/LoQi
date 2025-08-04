using FluentValidation;
using FluentValidation.AspNetCore;
using LoQi.API.BackgroundServices.Protocols;
using LoQi.API.BackgroundServices.Redis;
using LoQi.API.Validators;
using LoQi.Application.Services.Log;
using LoQi.Infrastructure.Extensions;
using LoQi.Persistence;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
            rollOnFileSizeLimit: true
        );
});

builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation().AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<LogSearchDtoValidator>();

// VALIDATION RESPONSE HANDLER - No exceptions, direct response!
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var correlationId = context.HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();

        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(e => new
            {
                field = x.Key,
                message = e.ErrorMessage,
                attemptedValue = x.Value.AttemptedValue
            }))
            .ToList();

        var response = new
        {
            success = false,
            error = "Validation failed",
            errorCode = "VALIDATION_ERROR",
            errors = errors,
            correlationId = correlationId,
            timestamp = DateTimeOffset.UtcNow
        };

        // Log validation errors (informational level)
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Validation failed for {RequestPath}: {Errors}",
            context.HttpContext.Request.Path,
            string.Join(", ", errors.Select(e => $"{e.field}: {e.message}")));

        return new BadRequestObjectResult(response);
    };
});

builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddScoped<ILogMapperService, LogMapperService>();

//builder.Services.AddSingleton<IUdpPackageListener, UdpPackageListener2>();
builder.Services.AddScoped<ILogService, LogService>();

// Background Services
builder.Services.AddHostedService<UdpPackageListener>();

// Redis Stream Configuration
builder.Services.AddRedisStream(builder.Configuration);
        
// Redis Stream Consumer Services
builder.Services.AddHostedService<ProcessedLogsConsumerService>();
builder.Services.AddHostedService<FailedLogsConsumerService>();
builder.Services.AddHostedService<RetryLogsConsumerService>();

// TODO: later
//builder.Services.AddAuthorization();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// TODO: to be restricted
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPolicy",
        policy =>
        {
            policy.WithOrigins("http://localhost:5003", "http://localhost:3000", "http://localhost:8080")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("DevPolicy");

app.MapHealthChecks("/health");

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 1 year
        if (ctx.File.Name.Contains(".") && !ctx.File.Name.EndsWith(".html"))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000";
        }
    }
});

app.UseRouting();
app.MapControllers();

// TODO: later
//app.UseAuthorization();

app.MapFallbackToFile("index.html");

// Initialize Redis Stream consumer groups
try
{
    await app.Services.InitializeRedisStreamAsync();
    app.Logger.LogInformation("Redis Stream consumer groups initialized successfully");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to initialize Redis Stream consumer groups");
    // App can still start, Redis streams will be created on first use
}

await app.RunAsync();