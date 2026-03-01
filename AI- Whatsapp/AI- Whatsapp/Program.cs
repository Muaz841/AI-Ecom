using EcomAI.Platform.Api.Extensions;
using EcomAI.Platform.Infrastructure.BackgroundJobs;
using EcomAI.Platform.Infrastructure.Logging;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, lc) => lc
    .Enrich.FromLogContext()
    .Enrich.With(services.GetRequiredService<TenantEnricher>())
    .MinimumLevel.Information()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.EnableAnnotations());
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
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

var scheduler = app.Services.GetRequiredService<HangfireJobScheduler>();
scheduler.RegisterAllJobs();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", (IWebHostEnvironment env) =>
    env.IsDevelopment() ? Results.Redirect("/swagger") : Results.Ok("API is running"));

app.Run();
