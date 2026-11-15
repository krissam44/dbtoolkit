using System.Data.Common;
using DbToolkit.Abstractions;
using DbToolkit.Exceptions;
using DbToolkit.Models;
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

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IUnitOfWork, Task<TResult>> work,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        policy ??= RetryPolicy.Default;

        for (var retry = 0; ; retry++)
        {
            try
            {
                // Libérée sans validation, la transaction est annulée : chaque
                // essai repart d'une base propre.
                await using var unitOfWork = await BeginAsync(cancellationToken);

                var result = await work(unitOfWork);

                await unitOfWork.CommitAsync(cancellationToken);

                return result;
            }
            catch (Exception exception) when (retry < policy.MaxRetries && IsTransient(exception))
            {
                var delay = policy.DelayFor(retry + 1);

                _logger.LogWarning(
                    exception,
                    "Transaction interrompue par un verrou ou un interblocage : reprise {Retry}/{MaxRetries} dans {Delay} ms.",
                    retry + 1,
                    policy.MaxRetries,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Erreur passagère, qu'elle vienne d'un repository (déjà classée) ou d'une
    /// requête exécutée directement sur la connexion de la transaction.
    /// </summary>
    private bool IsTransient(Exception exception) => exception switch
    {
        DataAccessException dataAccess => dataAccess.IsTransient,
        DbException db => _connectionFactory.Dialect.Classify(db)
            is DataAccessErrorKind.Busy or DataAccessErrorKind.Deadlock,
        _ => false
    };
}
