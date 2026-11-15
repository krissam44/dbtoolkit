using DbToolkit.Models;

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

    /// <summary>
    /// Exécute <paramref name="work"/> dans une transaction, la valide, et recommence
    /// le tout si le moteur signale un verrou ou un interblocage.
    /// <para>
    /// <paramref name="work"/> peut donc s'exécuter plusieurs fois : il ne doit rien
    /// faire d'irréversible en dehors de la base (envoi de courriel, appel externe)
    /// et doit relire dans la transaction les données dont il dépend.
    /// </para>
    /// </summary>
    /// <param name="policy">Politique de reprise ; <see cref="RetryPolicy.Default"/> si absente.</param>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IUnitOfWork, Task<TResult>> work,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default);
}

public static class UnitOfWorkFactoryExtensions
{
    /// <summary>
    /// Variante sans résultat de
    /// <see cref="IUnitOfWorkFactory.ExecuteInTransactionAsync{TResult}"/>.
    /// </summary>
    public static Task ExecuteInTransactionAsync(
        this IUnitOfWorkFactory factory,
        Func<IUnitOfWork, Task> work,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        return factory.ExecuteInTransactionAsync(
            async unitOfWork =>
            {
                await work(unitOfWork);
                return true;
            },
            policy,
            cancellationToken);
    }
}
