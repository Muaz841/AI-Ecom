using Microsoft.AspNetCore.Builder;
using Serilog;

namespace EcomAI.Platform.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseCoreMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseSerilogRequestLogging();
        app.UseCors("AllowDevelopment");
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
