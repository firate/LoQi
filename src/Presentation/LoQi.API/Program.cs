using LoQi.API.BackgroundServices;
using LoQi.API.Hubs;
using LoQi.API.Services;
using LoQi.Application;
using LoQi.Application.Service;
using LoQi.Persistence;
using Microsoft.AspNetCore.Http.Connections;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.AddScoped<INotificationService, SignalRNotification>();

// TODO: later
//builder.Services.AddAuthorization();

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// TODO: to be restricted
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

builder.Services.AddHostedService<UdpListenerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseDefaultFiles();
app.UseStaticFiles();

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