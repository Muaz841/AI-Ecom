using EcomAI.Platform.Api.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Information()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDevelopment", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddCoreInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseCoreMiddleware();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", (IWebHostEnvironment env) =>
    env.IsDevelopment() ? Results.Redirect("/swagger") : Results.Ok("API is running"));

app.Run();
