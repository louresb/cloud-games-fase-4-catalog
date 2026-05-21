using System.Reflection;
using Fiap.CloudGames.Domain.Carts.Repositories;
using Fiap.CloudGames.Domain.Games.Repositories;
using Fiap.CloudGames.Domain.Games.Search;
using Fiap.CloudGames.Domain.Orders.Repositories;
using Fiap.CloudGames.Domain.Promotions.Repositories;
using Fiap.CloudGames.Domain.UserGamesLibrary.Repositories;
using Fiap.CloudGames.Infrastructure.Carts.Repositories;
using Fiap.CloudGames.Infrastructure.Games.Repositories;
using Fiap.CloudGames.Infrastructure.Games.Search;
using Fiap.CloudGames.Infrastructure.Games.Seeders;
using Fiap.CloudGames.Infrastructure.Orders.Repositories;
using Fiap.CloudGames.Infrastructure.Persistence;
using Fiap.CloudGames.Infrastructure.Promotions.Repositories;
using Fiap.CloudGames.Infrastructure.UserGamesLibrary.Repositories;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;
using OpenSearch.Net;

namespace Fiap.CloudGames.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration,
        Assembly? consumersAssembly = null,
        Type[]? consumerCommandTypes = null,
        Type[]? consumerEventTypes = null)
    {
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUserGameLibraryRepository, UserGameLibraryRepository>();
        
        services.AddScoped<IGameSeeder, GameSeeder>();

        var catalogCommandsQueue = configuration["Queues:Catalog:Commands"] ?? throw new InvalidOperationException("Catalog commands queue not configured.");
        var catalogEventsQueue = configuration["Queues:Catalog:Events"] ?? throw new InvalidOperationException("Catalog events queue not configured.");

        consumerCommandTypes = consumerCommandTypes?.Where(t => typeof(IConsumer).IsAssignableFrom(t)).ToArray() ?? [];
        consumerEventTypes = consumerEventTypes?.Where(t => typeof(IConsumer).IsAssignableFrom(t)).ToArray() ?? [];

        services.AddMassTransit(x =>
        {
            // Descobre automaticamente os Consumers na camada de Application
            if (consumersAssembly is not null)
            {
                x.AddConsumers(consumersAssembly);
            }

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitHost = configuration["RabbitMq:HostName"] ?? "localhost";
                var rabbitUser = configuration["RabbitMq:UserName"] ?? "guest";
                var rabbitPass = configuration["RabbitMq:Password"] ?? "guest";

                cfg.Host(rabbitHost, "/", h =>
                {
                    h.ConnectionName("Fiap.CloudGames.Catalog.API");
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ReceiveEndpoint(catalogCommandsQueue, e =>
                {
                    foreach (var consumerType in consumerCommandTypes)
                    {
                        if (!typeof(IConsumer).IsAssignableFrom(consumerType))
                            throw new InvalidOperationException($"Type {consumerType.FullName} is not a MassTransit consumer.");

                        e.ConfigureConsumer(context, consumerType);
                    }
                });
                
                cfg.ReceiveEndpoint(catalogEventsQueue, e =>
                {
                    foreach (var consumerType in consumerEventTypes)
                    {
                        if (!typeof(IConsumer).IsAssignableFrom(consumerType))
                            throw new InvalidOperationException($"Type {consumerType.FullName} is not a MassTransit consumer.");

                        e.ConfigureConsumer(context, consumerType);
                    }
                });

                cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(2)));
            });
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);

                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            });
        });

        services.AddDataProtection()
            .SetApplicationName("Fiap.CloudGames")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: connectionString,
                name: "sqlserver",
                tags: new[] { "db", "data" });

        AddRedis(services, configuration);
        AddOpenSearch(services, configuration);

        return services;
    }

    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connection))
        {
            // No Redis configured → use in-memory cache so the service still runs locally.
            services.AddDistributedMemoryCache();
            return;
        }

        services.AddStackExchangeRedisCache(o =>
        {
            o.Configuration = connection;
            o.InstanceName = configuration["Redis:InstanceName"] ?? "catalog:";
        });
    }

    private static void AddOpenSearch(IServiceCollection services, IConfiguration configuration)
    {
        var uri = configuration["OpenSearch:Uri"];

        if (string.IsNullOrWhiteSpace(uri))
        {
            services.AddSingleton<IGameSearchService, NullGameSearchService>();
            return;
        }

        var options = new OpenSearchOptions
        {
            Uri = uri,
            IndexName = configuration["OpenSearch:IndexName"] ?? "games",
            Username = configuration["OpenSearch:Username"],
            Password = configuration["OpenSearch:Password"],
            DisableSslCertValidation = bool.TryParse(configuration["OpenSearch:DisableSslCertValidation"], out var skip) && skip
        };

        services.AddSingleton(options);

        services.AddSingleton<IOpenSearchClient>(_ =>
        {
            var settings = new ConnectionSettings(new Uri(options.Uri))
                .DefaultIndex(options.IndexName);

            if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
            {
                settings = settings.BasicAuthentication(options.Username, options.Password);
            }

            if (options.DisableSslCertValidation)
            {
                settings = settings.ServerCertificateValidationCallback((_, _, _, _) => true);
            }

            return new OpenSearchClient(settings);
        });

        services.AddSingleton<IGameSearchService, OpenSearchGameSearchService>();
        services.AddHostedService<SearchIndexInitializer>();
    }
}
