using System.Data.Common;
using DbToolkit.Abstractions;
using Microsoft.Extensions.Logging;

namespace DbToolkit.Infrastructure;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly ILogger<UnitOfWork> _logger;

    private bool _completed;
    private bool _disposed;

    internal UnitOfWork(
        DbConnection connection,
        DbTransaction transaction,
        ILogger<UnitOfWork> logger)
    {
        _connection = connection;
        _transaction = transaction;
        _logger = logger;
    }

    public DbConnection Connection
    {
        get
        {
            EnsureActive();
            return _connection;
        }
    }

    public DbTransaction Transaction
    {
        get
        {
            EnsureActive();
            return _transaction;
        }
    }

    public async Task CommitAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureActive();

        await _transaction.CommitAsync(cancellationToken);

        _logger.LogDebug("Transaction validée.");

        _completed = true;
    }

    public async Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureActive();

        await _transaction.RollbackAsync(cancellationToken);

        _logger.LogDebug("Transaction annulée.");

        _completed = true;
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_completed)
        {
            throw new InvalidOperationException(
                "La transaction est terminée (déjà validée ou annulée).");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            try
            {
                if (!_completed)
                {
                    _logger.LogWarning("Transaction non validée : rollback automatique.");

                    await _transaction.RollbackAsync();
                }
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }
}
