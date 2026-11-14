using DbToolkit.Abstractions;
using Microsoft.Extensions.Logging;

namespace DbToolkit.Infrastructure;

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

            var transaction = await connection.BeginTransactionAsync(cancellationToken);

            _logger.LogDebug("Transaction démarrée.");

            return new UnitOfWork(connection, transaction, _logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
