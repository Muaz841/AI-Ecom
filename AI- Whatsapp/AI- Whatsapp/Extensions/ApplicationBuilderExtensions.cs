using Microsoft.AspNetCore.Builder;
using Hangfire;
using EcomAI.Platform.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using EcomAI.Platform.Api.Middleware;
using EcomAI.Platform.Infrastructure.Realtime;

namespace EcomAI.Platform.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseCoreMiddleware(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        //app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseSerilogRequestLogging();
        app.UseCors("AllowDevelopment");
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication UseBackgroundProcessing(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseHangfireDashboard("/hangfire");
        }

        var scheduler = app.Services.GetRequiredService<HangfireJobScheduler>();
        scheduler.RegisterAllJobs();

        return app;
    }

    public static WebApplication MapCoreEndpoints(this WebApplication app)
    {
        app.MapControllers();
        app.MapHub<RealtimeHub>("/hubs/realtime");
        app.MapHealthChecks("/health");
        app.MapGet("/", (IWebHostEnvironment env) =>
            env.IsDevelopment() ? Results.Redirect("/swagger") : Results.Ok("API is running"));

        return app;
    }
}
