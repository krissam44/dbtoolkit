using Microsoft.Extensions.DependencyInjection;
using SqliteToolkit.Abstractions;
using SqliteToolkit.Configuration;
using SqliteToolkit.Infrastructure;
using SqliteToolkit.Migrations;

namespace SqliteToolkit;

public static class DependencyInjection
{
    public static IServiceCollection AddSqliteToolkit(this IServiceCollection services, Action<SqliteOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<
            IDbConnectionFactory,        
            SqliteConnectionFactory>();

        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.AddScoped<MigrationRunner>();

        return services;
    }

    public static IServiceCollection AddRepository<TInterface, TImplementation>(
    this IServiceCollection services)
    where TInterface : class
    where TImplementation : class, TInterface
    {
        services.AddScoped<TInterface, TImplementation>();

        return services;
    }

}