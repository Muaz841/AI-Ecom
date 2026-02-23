using Microsoft.AspNetCore.Builder;

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
        app.UseCors("AllowDevelopment");
        app.UseAuthorization();

        return app;
    }
}
