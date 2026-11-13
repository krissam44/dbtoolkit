using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SqliteToolkit.Abstractions;
using SqliteToolkit.Exceptions;
using SqliteToolkit.Models;
using System.Runtime.CompilerServices;

namespace SqliteToolkit.Repositories;

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
    /// SQLite à trier toute la table à chaque comptage.
    /// </summary>
    protected abstract string GetAllSql { get; }

    protected abstract string InsertSql { get; }
    protected abstract string UpdateSql { get; }
    protected abstract string DeleteSql { get; }

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
    /// Les <see cref="SqliteException"/> sont journalisées et converties
    /// en <see cref="DataAccessException"/>.
    /// À utiliser aussi pour les requêtes spécifiques des repositories dérivés.
    /// </summary>
    protected async Task<TResult> ExecuteAsync<TResult>(
        Func<SqliteConnection, SqliteTransaction?, Task<TResult>> operation,
        IUnitOfWork? unitOfWork,
        CancellationToken cancellationToken,
        [CallerMemberName] string operationName = "")
    {
        SqliteConnection? ownedConnection = null;

        try
        {
            SqliteConnection db;
            SqliteTransaction? transaction;

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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Erreur lors de l'opération {Operation} dans le repository {Repository}",
                operationName,
                GetType().Name);

            if (ex is SqliteException)
            {
                throw new DataAccessException(
                    $"Erreur SQLite lors de l'opération {operationName}.",
                    ex);
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

                return affected > 0;
            },
            unitOfWork,
            cancellationToken);
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
