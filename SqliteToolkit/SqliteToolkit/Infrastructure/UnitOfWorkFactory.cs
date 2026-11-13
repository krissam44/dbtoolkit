using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SqliteToolkit.Abstractions;

namespace SqliteToolkit.Infrastructure;

public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWorkFactory(
        IDbConnectionFactory connectionFactory,
        ILogger<UnitOfWork> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IUnitOfWork> BeginAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = _connectionFactory.CreateConnection();

        try
        {
            await connection.OpenAsync(cancellationToken);

            var transaction = (SqliteTransaction)
                await connection.BeginTransactionAsync(cancellationToken);

            _logger.LogInformation("Transaction SQLite démarrée.");

            return new UnitOfWork(connection, transaction, _logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
