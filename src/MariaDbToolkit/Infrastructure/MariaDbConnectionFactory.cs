using System.Data.Common;
using DbToolkit.Abstractions;
using MariaDbToolkit.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace MariaDbToolkit.Infrastructure;

public sealed class MariaDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MariaDbConnectionFactory(IOptions<MariaDbOptions> options)
    {
        var value = options.Value;

        if (string.IsNullOrWhiteSpace(value.ConnectionString))
        {
            throw new InvalidOperationException(
                "La chaîne de connexion MariaDB n'est pas configurée.");
        }

        var builder = new MySqlConnectionStringBuilder(value.ConnectionString)
        {
            DefaultCommandTimeout = (uint)Math.Max(value.DefaultCommandTimeout, 0),

            // Un UPDATE qui ne change aucune valeur doit compter comme
            // « ligne trouvée », comme sur SQLite : UpdateAsync renvoie alors true.
            UseAffectedRows = false
        };

        _connectionString = builder.ConnectionString;
    }

    public ISqlDialect Dialect => MariaDbDialect.Instance;

    public DbConnection CreateConnection()
        => new MySqlConnection(_connectionString);
}
