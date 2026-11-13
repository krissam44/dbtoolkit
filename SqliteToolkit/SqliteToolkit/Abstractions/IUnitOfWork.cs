using Microsoft.Data.Sqlite;

namespace SqliteToolkit.Abstractions;

/// <summary>
/// Transaction SQLite en cours, créée par <see cref="IUnitOfWorkFactory"/>.
/// Si elle est libérée sans <see cref="CommitAsync"/>, elle est annulée.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Connexion de la transaction. Lève une exception si la transaction est terminée.</summary>
    SqliteConnection Connection { get; }

    /// <summary>Transaction active. Lève une exception si elle est terminée.</summary>
    SqliteTransaction Transaction { get; }

    Task CommitAsync(
        CancellationToken cancellationToken = default);

    Task RollbackAsync(
        CancellationToken cancellationToken = default);
}
