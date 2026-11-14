namespace DbToolkit.Abstractions;

/// <summary>
/// Crée une transaction courte par opération métier.
/// </summary>
public interface IUnitOfWorkFactory
{
    /// <summary>
    /// Ouvre une connexion et démarre une transaction.
    /// À utiliser avec <c>await using</c>.
    /// </summary>
    Task<IUnitOfWork> BeginAsync(
        CancellationToken cancellationToken = default);
}
