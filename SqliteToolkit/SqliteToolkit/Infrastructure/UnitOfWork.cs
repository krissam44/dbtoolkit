using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SqliteToolkit.Abstractions;

namespace SqliteToolkit.Infrastructure;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private readonly ILogger<UnitOfWork> _logger;

    private bool _completed;
    private bool _disposed;

    internal UnitOfWork(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ILogger<UnitOfWork> logger)
    {
        _connection = connection;
        _transaction = transaction;
        _logger = logger;
    }

    public SqliteConnection Connection
    {
        get
        {
            EnsureActive();
            return _connection;
        }
    }

    public SqliteTransaction Transaction
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

        _logger.LogInformation("Transaction SQLite validée.");

        _completed = true;
    }

    public async Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureActive();

        await _transaction.RollbackAsync(cancellationToken);

        _logger.LogWarning("Transaction SQLite annulée.");

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
