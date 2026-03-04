using EcomAI.Platform.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.AddStructuredLogging();
builder.Services.AddApiServices(builder.Configuration, builder.Environment);
builder.Services.AddCoreInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseCoreMiddleware();
app.UseBackgroundProcessing();
app.MapCoreEndpoints();

app.Run();
