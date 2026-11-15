using DbToolkit.Models;

namespace DbToolkit.Abstractions;

/// <summary>
/// Chaque méthode accepte un <see cref="IUnitOfWork"/> optionnel :
/// fourni, l'opération s'exécute dans sa transaction ;
/// absent, elle ouvre et ferme sa propre connexion.
/// </summary>
public interface IRepository<TEntity, TKey>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(
        TKey id,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renvoie une page : pagination, tris, filtres et recherche
    /// du <paramref name="request"/> sont appliqués en SQL.
    /// </summary>
    Task<PagedResult<TEntity>> GetPagedAsync(
        PageRequest request,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default);

    Task<TKey> InsertAsync(
        TEntity entity,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        TEntity entity,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        TKey id,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default);
}
