using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SqliteToolkit.Abstractions;
using SqliteToolkit.Configuration;

namespace SqliteToolkit.Infrastructure;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly SqliteOptions _options;

    public SqliteConnectionFactory(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    public SqliteConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "La chaîne de connexion SQLite n'est pas configurée.");
        }

        var builder = new SqliteConnectionStringBuilder(
            _options.ConnectionString)
        {
            DefaultTimeout = _options.DefaultTimeout,
            ForeignKeys = _options.ForeignKeys,
            Pooling = true
        };

        return new SqliteConnection(builder.ConnectionString);
    }
}