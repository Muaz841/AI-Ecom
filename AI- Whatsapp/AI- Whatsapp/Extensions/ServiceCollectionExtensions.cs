using EcomAI.Platform.Infrastructure.Tenant;
using Microsoft.Extensions.DependencyInjection;

namespace EcomAI.Platform.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();

        return services;
    }
}
