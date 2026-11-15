using DbToolkit;
using DbToolkit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using SqliteToolkit.Configuration;
using SqliteToolkit.Infrastructure;

namespace SqliteToolkit;

public static class DependencyInjection
{
    /// <summary>
    /// Enregistre DbToolkit avec SQLite comme moteur.
    /// </summary>
    public static IServiceCollection AddSqliteToolkit(
        this IServiceCollection services,
        Action<SqliteOptions> configure)
    {
        services.Configure(configure);

        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();

        return services.AddDbToolkitCore();
    }
}
