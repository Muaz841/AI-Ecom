using System;
using EcomAI.Platform.Business.Common.Behaviors;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Infrastructure.ExternalServices;
using EcomAI.Platform.Infrastructure.BackgroundJobs;
using EcomAI.Platform.Infrastructure.Logging;
using EcomAI.Platform.Infrastructure.Persistence;
using EcomAI.Platform.Infrastructure.Persistence.Repositories;
using EcomAI.Platform.Infrastructure.Tenant;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Serilog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Annotations;
using EcomAI.Platform.Infrastructure.Security;
using EcomAI.Platform.Api.Security;
using EcomAI.Platform.Business.Security;
using System.Text;

namespace EcomAI.Platform.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c => c.EnableAnnotations());

        services.Configure<JwtAuthSettings>(configuration.GetSection("Authentication:Jwt"));
        var jwt = configuration.GetSection("Authentication:Jwt").Get<JwtAuthSettings>() ?? new JwtAuthSettings();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionCodes.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddCors(options =>
        {
            options.AddPolicy("AllowDevelopment", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        services.AddHealthChecks();

        return services;
    }

    public static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        
        services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
        services.AddHttpContextAccessor();

        services.AddDbContext<PlatformDbContext>(options =>
        {
            var connString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseSqlServer(connString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 5);
                sqlOptions.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
            });

            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<ClientRepository>();
        services.AddScoped<IRepository<Client>, ClientRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationThreadRepository, ConversationThreadRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ScheduledPostRepository>();
        services.AddScoped<IMetaMessagingService, MetaMessagingService>();
        services.AddScoped<IAuthService, JwtAuthService>();
        services.AddScoped<IRbacService, RbacService>();
        services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddScoped<IApplicationLogger, ApplicationLogger>();
        services.Configure<AISettings>(configuration.GetSection("AI"));
        services.AddSingleton<TenantEnricher>();
        services.AddScoped<MockAIService>();
        services.AddScoped<OpenAIService>();
        services.AddScoped<GeminiService>();
        services.AddScoped<OllamaService>();
        services.AddScoped<IAIService, AIServiceFactory>();
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
        services.Configure<BootstrapSettings>(configuration.GetSection("Bootstrap"));

        var defaultConnection = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(defaultConnection, new SqlServerStorageOptions()));
        services.AddHangfireServer();

        services.AddScoped<PublishScheduledPostsJob>();
        services.AddScoped<SendFollowUpRemindersJob>();
        services.AddSingleton<HangfireJobScheduler>();
        services.AddHostedService<RbacSeedHostedService>();

        return services;
    }
}

public static class HostBuilderExtensions
{
    public static ConfigureHostBuilder AddStructuredLogging(this ConfigureHostBuilder host)
    {
        host.UseSerilog((ctx, services, lc) => lc
            .Enrich.FromLogContext()
            .Enrich.With(services.GetRequiredService<TenantEnricher>())
            .MinimumLevel.Information()
            .WriteTo.Console());

        return host;
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
