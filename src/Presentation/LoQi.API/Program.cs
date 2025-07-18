using LoQi.API.BackgroundServices;
using LoQi.API.Hubs;
using LoQi.API.Services;
using LoQi.Application.Services;
using LoQi.Persistence;
using Microsoft.AspNetCore.Http.Connections;
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
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

builder.Services.AddPersistenceServices(builder.Configuration);

builder.Services.AddSingleton<IUdpPackageListener, UdpPackageListener>();
builder.Services.AddScoped<INotificationService, SignalRNotification>();
builder.Services.AddScoped<ILogService, LogService>();

// builder.Services.AddApplicationServices(builder.Configuration);



// TODO: later
//builder.Services.AddAuthorization();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// TODO: to be restricted
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.WithOrigins("http://localhost:5003")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

builder.Services.AddHostedService<UdpLogProcessingService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

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

app.MapHub<LogHub>("/loghub", options =>
{
    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
});

app.MapFallbackToFile("index.html");

await app.RunAsync();
