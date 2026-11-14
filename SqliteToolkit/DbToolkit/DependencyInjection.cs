using DbToolkit.Abstractions;
using DbToolkit.Infrastructure;
using DbToolkit.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DbToolkit;

public static class DependencyInjection
{
    /// <summary>
    /// Services communs à tous les fournisseurs. Appelé par
    /// <c>AddSqliteToolkit</c> et <c>AddMariaDbToolkit</c>, qui enregistrent
    /// en plus leur <see cref="IDbConnectionFactory"/>.
    /// </summary>
    public static IServiceCollection AddDbToolkitCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.TryAddScoped<MigrationRunner>();

        return services;
    }
}
