using System;
using EcomAI.Platform.Business.Common.Behaviors;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.ExternalServices;
using EcomAI.Platform.Infrastructure.Persistence;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using EcomAI.Platform.Infrastructure.Tenant;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace EcomAI.Platform.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();

        services.AddDbContext<PlatformDbContext>(options =>
        {
            var connString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseSqlServer(connString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 5);
                sqlOptions.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
            });


            options.EnableSensitiveDataLogging();

        });

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<ClientRepository>();
        services.AddScoped<IRepository<Client>, ClientRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationThreadRepository, ConversationThreadRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IMetaMessagingService, MetaMessagingService>();
        services.AddScoped<IAIService, StubAIService>();
        services.AddHttpClient(MetaMessagingService.MetaHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://graph.facebook.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddResiliencePipelineRegistry<string>();
        services.AddResiliencePipeline(MetaMessagingService.SendMessagePipeline, builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
            });
        });
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(typeof(ProcessIncomingMessageCommand).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(ProcessIncomingMessageCommand).Assembly);
        services.AddOptions<MetaSecrets>()
            .Configure<IConfiguration>((secrets, cfg) =>
            {
                secrets.AppSecret = cfg["MetaSecrets:AppSecret"]
                    ?? throw new InvalidOperationException("Meta App Secret not found in secure storage.");
            })
            .ValidateDataAnnotations();
        services.AddSingleton<IValidateOptions<MetaSecrets>, MetaSecretsValidator>();

        return services;
    }
}

public class MetaSecrets
{
    public string AppSecret { get; set; } = null!;
}

public class MetaSecretsValidator : IValidateOptions<MetaSecrets>
{
    public ValidateOptionsResult Validate(string? name, MetaSecrets options)
    {
        if (string.IsNullOrWhiteSpace(options.AppSecret))
        {
            return ValidateOptionsResult.Fail("Meta App Secret cannot be empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
