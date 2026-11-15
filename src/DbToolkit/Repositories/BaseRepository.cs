using System.Data.Common;
using System.Runtime.CompilerServices;
using Dapper;
using DbToolkit.Abstractions;
using DbToolkit.Exceptions;
using DbToolkit.Models;
using Microsoft.Extensions.Logging;

namespace DbToolkit.Repositories;

public abstract class BaseRepository<TEntity, TKey>
    : IRepository<TEntity, TKey>
    where TEntity : class
{
    private readonly IDbConnectionFactory _factory;

    private readonly ILogger _logger;

    protected BaseRepository(
        IDbConnectionFactory factory,
        ILogger logger)
    {
        _factory = factory;
        _logger = logger;
    }

    protected abstract string GetByIdSql { get; }

    /// <summary>
    /// Requête source de <see cref="GetAllAsync"/> et de <see cref="GetPagedAsync"/>.
    /// À écrire sans ORDER BY : le tri vient de <see cref="DefaultOrderBy"/>
    /// ou de la demande de page. Un ORDER BY ici reste correct mais oblige
    /// le moteur à trier toute la table à chaque comptage.
    /// </summary>
    protected abstract string GetAllSql { get; }

    protected abstract string InsertSql { get; }
    protected abstract string UpdateSql { get; }
    protected abstract string DeleteSql { get; }

    // ---------------------------------------------------------------------------------
    // Accès concurrents. Pour qu'un utilisateur n'écrase pas la saisie d'un autre,
    // la table porte un numéro de version et UpdateSql le vérifie et l'incrémente :
    //   UPDATE t SET ..., version = version + 1 WHERE id = @Id AND version = @Version

    /// <summary>
    /// Requête paramétrée par l'entité (<c>@Id</c>...) qui renvoie une ligne si
    /// l'entité existe encore, par exemple <c>SELECT 1 FROM t WHERE id = @Id</c>.
    /// Définie, elle permet à <see cref="UpdateAsync"/> et à
    /// <see cref="DeleteVersionedAsync"/> de distinguer une ligne disparue (résultat
    /// <c>false</c>) d'une ligne modifiée par quelqu'un d'autre
    /// (<see cref="ConcurrencyException"/>). Absente : les deux cas renvoient <c>false</c>.
    /// </summary>
    protected virtual string? ConcurrencyCheckSql => null;

    /// <summary>
    /// Suppression gardée par la version, paramétrée par l'entité :
    /// <c>DELETE FROM t WHERE id = @Id AND version = @Version</c>.
    /// Nécessaire à <see cref="DeleteVersionedAsync"/>.
    /// </summary>
    protected virtual string? DeleteVersionedSql => null;

    /// <summary>
    /// Reprise des opérations exécutées hors transaction quand le moteur signale un
    /// verrou ou un interblocage. Dans une transaction, c'est
    /// <see cref="IUnitOfWorkFactory.ExecuteInTransactionAsync{TResult}"/> qui reprend le tout.
    /// </summary>
    protected virtual RetryPolicy RetryPolicy => RetryPolicy.Default;

    // ---------------------------------------------------------------------------------
    // Configuration de la pagination. Les colonnes SQL désignent
    // celles renvoyées par GetAllSql.

    /// <summary>Colonnes triables : nom logique → colonne SQL.</summary>
    protected virtual IReadOnlyDictionary<string, string> AllowedSortColumns
        => new Dictionary<string, string>();

    /// <summary>Colonnes filtrables : nom logique → colonne SQL.</summary>
    protected virtual IReadOnlyDictionary<string, string> AllowedFilterColumns
        => new Dictionary<string, string>();

    /// <summary>Colonnes SQL parcourues par la recherche globale.</summary>
    protected virtual IReadOnlyList<string> SearchColumns
        => [];

    /// <summary>Tri appliqué quand la demande n'en précise aucun (sans le mot-clé ORDER BY).</summary>
    protected virtual string DefaultOrderBy
        => string.Empty;

    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Exécute une opération sur la connexion du Unit of Work s'il est fourni,
    /// sinon sur une connexion ouverte puis libérée pour l'occasion.
    /// Les erreurs du moteur (<see cref="DbException"/>) sont journalisées et
    /// converties en <see cref="DataAccessException"/>, avec leur nature.
    /// À utiliser aussi pour les requêtes spécifiques des repositories dérivés.
    /// <para>
    /// Sans Unit of Work, une erreur passagère (verrou, interblocage) relance
    /// <paramref name="operation"/> selon <see cref="RetryPolicy"/> : le moteur a annulé
    /// l'instruction, la rejouer est sans risque. Avec un Unit of Work, l'erreur remonte,
    /// car c'est toute la transaction qu'il faut reprendre.
    /// </para>
    /// </summary>
    protected async Task<TResult> ExecuteAsync<TResult>(
        Func<DbConnection, DbTransaction?, Task<TResult>> operation,
        IUnitOfWork? unitOfWork,
        CancellationToken cancellationToken,
        [CallerMemberName] string operationName = "")
    {
        var policy = RetryPolicy;

        for (var retry = 0; ; retry++)
        {
            try
            {
                return await ExecuteOnceAsync(operation, unitOfWork, cancellationToken, operationName);
            }
            catch (DataAccessException exception)
                when (unitOfWork is null && exception.IsTransient && retry < policy.MaxRetries)
            {
                var delay = policy.DelayFor(retry + 1);

                _logger.LogWarning(
                    "Opération {Operation} du repository {Repository} bloquée par un verrou : reprise {Retry}/{MaxRetries} dans {Delay} ms.",
                    operationName,
                    GetType().Name,
                    retry + 1,
                    policy.MaxRetries,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task<TResult> ExecuteOnceAsync<TResult>(
        Func<DbConnection, DbTransaction?, Task<TResult>> operation,
        IUnitOfWork? unitOfWork,
        CancellationToken cancellationToken,
        string operationName)
    {
        DbConnection? ownedConnection = null;

        try
        {
            DbConnection db;
            DbTransaction? transaction;

            if (unitOfWork != null)
            {
                db = unitOfWork.Connection;
                transaction = unitOfWork.Transaction;
            }
            else
            {
                ownedConnection = _factory.CreateConnection();

                await ownedConnection.OpenAsync(cancellationToken);

                db = ownedConnection;
                transaction = null;
            }

            return await operation(db, transaction);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyException)
        {
            _logger.LogError(
                ex,
                "Erreur lors de l'opération {Operation} dans le repository {Repository}",
                operationName,
                GetType().Name);

            if (ex is DbException dbException)
            {
                var dialect = _factory.Dialect;

                throw new DataAccessException(
                    $"Erreur {dialect.Name} lors de l'opération {operationName}.",
                    ex,
                    dialect.Classify(dbException));
            }

            throw;
        }
        finally
        {
            if (ownedConnection != null)
                await ownedConnection.DisposeAsync();
        }
    }

    // ---------------------------------------------------------------------------------

    public Task<TEntity?> GetByIdAsync(
        TKey id,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            (db, transaction) => db.QuerySingleOrDefaultAsync<TEntity>(
                new CommandDefinition(
                    GetByIdSql,
                    new { Id = id },
                    transaction: transaction,
                    cancellationToken: cancellationToken)),
            unitOfWork,
            cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<IReadOnlyList<TEntity>> GetAllAsync(
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default)
    {
        // Même encapsulation que la pagination : DefaultOrderBy
        // désigne les colonnes renvoyées par GetAllSql.
        var sql = string.IsNullOrWhiteSpace(DefaultOrderBy)
            ? GetAllSql
            : $"""
                SELECT *
                FROM ({GetAllSql.Trim().TrimEnd(';')}) AS AllQuery
                ORDER BY {DefaultOrderBy};
                """;

        return ExecuteAsync<IReadOnlyList<TEntity>>(
            async (db, transaction) =>
            {
                var result = await db.QueryAsync<TEntity>(
                    new CommandDefinition(
                        sql,
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                return result.ToList();
            },
            unitOfWork,
            cancellationToken);
    }

    // ---------------------------------------------------------------------------------

    public Task<PagedResult<TEntity>> GetPagedAsync(
        PageRequest request,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = PagedQueryBuilder.Build(
            GetAllSql,
            request,
            AllowedSortColumns,
            AllowedFilterColumns,
            SearchColumns,
            DefaultOrderBy);

        return ExecuteAsync(
            async (db, transaction) =>
            {
                var totalCount = await db.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        query.CountSql,
                        query.Parameters,
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                var items = await db.QueryAsync<TEntity>(
                    new CommandDefinition(
                        query.ItemsSql,
                        query.Parameters,
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                return new PagedResult<TEntity>
                {
                    Items = items.ToList(),
                    PageNumber = request.Skip / request.Take + 1,
                    PageSize = request.Take,
                    TotalCount = totalCount
                };
            },
            unitOfWork,
            cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<TKey> InsertAsync(
        TEntity entity,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            (db, transaction) => db.QuerySingleAsync<TKey>(
                new CommandDefinition(
                    InsertSql,
                    entity,
                    transaction: transaction,
                    cancellationToken: cancellationToken)),
            unitOfWork,
            cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<bool> UpdateAsync(
        TEntity entity,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async (db, transaction) =>
            {
                var affected = await db.ExecuteAsync(
                    new CommandDefinition(
                        UpdateSql,
                        entity,
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                if (affected > 0)
                    return true;

                await ThrowIfStillExistsAsync(db, transaction, entity, cancellationToken);

                return false;
            },
            unitOfWork,
            cancellationToken);
    }

    /// <summary>
    /// Supprime l'entité si elle est encore dans la version lue (<see cref="DeleteVersionedSql"/>).
    /// Renvoie <c>false</c> si elle n'existe plus.
    /// </summary>
    /// <exception cref="ConcurrencyException">
    /// La ligne a été modifiée depuis sa lecture (si <see cref="ConcurrencyCheckSql"/> est défini).
    /// </exception>
    public Task<bool> DeleteVersionedAsync(
        TEntity entity,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var sql = DeleteVersionedSql
            ?? throw new NotSupportedException(
                $"{GetType().Name} ne définit pas DeleteVersionedSql.");

        return ExecuteAsync(
            async (db, transaction) =>
            {
                var affected = await db.ExecuteAsync(
                    new CommandDefinition(
                        sql,
                        entity,
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                if (affected > 0)
                    return true;

                await ThrowIfStillExistsAsync(db, transaction, entity, cancellationToken);

                return false;
            },
            unitOfWork,
            cancellationToken);
    }

    /// <summary>
    /// Après une écriture gardée par la version qui n'a touché aucune ligne :
    /// si la ligne existe encore, c'est qu'un autre l'a modifiée.
    /// </summary>
    private async Task ThrowIfStillExistsAsync(
        DbConnection db,
        DbTransaction? transaction,
        TEntity entity,
        CancellationToken cancellationToken)
    {
        if (ConcurrencyCheckSql is not { } sql)
            return;

        var rows = await db.QueryAsync<object>(
            new CommandDefinition(
                sql,
                entity,
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (rows.Any())
        {
            throw new ConcurrencyException(
                "La ligne a été modifiée par un autre utilisateur depuis sa lecture.");
        }
    }

    // ---------------------------------------------------------------------------------
    public Task<bool> DeleteAsync(
        TKey id,
        IUnitOfWork? unitOfWork = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async (db, transaction) =>
            {
                var affected = await db.ExecuteAsync(
                    new CommandDefinition(
                        DeleteSql,
                        new { Id = id },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                return affected > 0;
            },
            unitOfWork,
            cancellationToken);
    }
}
