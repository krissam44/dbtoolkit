using System.Data.Common;

namespace DbToolkit.Abstractions;

/// <summary>
/// Transaction en cours, créée par <see cref="IUnitOfWorkFactory"/>.
/// Si elle est libérée sans <see cref="CommitAsync"/>, elle est annulée.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Connexion de la transaction. Lève une exception si la transaction est terminée.</summary>
    DbConnection Connection { get; }

    /// <summary>Transaction active. Lève une exception si elle est terminée.</summary>
    DbTransaction Transaction { get; }

    Task CommitAsync(
        CancellationToken cancellationToken = default);

    Task RollbackAsync(
        CancellationToken cancellationToken = default);
}
