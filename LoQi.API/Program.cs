using LoQi.Application;
using LoQi.Persistence;
using Scalar.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);


// Add services to the container.
//builder.Services.AddAuthorization();

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// TODO: any kullanımı kısıtlanmalı.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // await using var scope = app.Services.CreateAsyncScope();
    // var seeder = scope.ServiceProvider.GetRequiredService<Seeder>();
    // await seeder.CreateLogTableAsync();
}

app.UseHttpsRedirection();


app.UseCors("AllowAll");

//app.UseAuthorization();

app.MapControllers();

await app.RunAsync();