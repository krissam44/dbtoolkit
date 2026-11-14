using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqliteToolkit.Abstractions;
using SqliteToolkit.Repositories;

namespace SqliteToolkit.Tests;

/// <summary>
/// Base SQLite jetable : un fichier temporaire par test, supprimé à la fin.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path;
    private readonly ServiceProvider _provider;

    private TestDatabase(string path, ServiceProvider provider)
    {
        _path = path;
        _provider = provider;
    }

    public IUnitOfWorkFactory UnitOfWork
        => _provider.GetRequiredService<IUnitOfWorkFactory>();

    public ItemRepository Items
        => _provider.GetRequiredService<ItemRepository>();

    public BareItemRepository BareItems
        => _provider.GetRequiredService<BareItemRepository>();

    public static async Task<TestDatabase> CreateAsync()
    {
        var path = NewPath();

        var services = new ServiceCollection();
        services.AddLogging();
        ConfigureServices(services, path);

        var provider = services.BuildServiceProvider();

        await CreateSchemaAsync(provider);

        return new TestDatabase(path, provider);
    }

    public static string NewPath()
        => Path.Combine(
            Path.GetTempPath(),
            $"sqlitetoolkit_tests_{Guid.NewGuid():N}.db");

    public static void ConfigureServices(IServiceCollection services, string path)
    {
        services.AddSqliteToolkit(options => options.ConnectionString = $"Data Source={path}");
        services.AddSingleton<ItemRepository>();
        services.AddSingleton<BareItemRepository>();
    }

    public static async Task CreateSchemaAsync(IServiceProvider provider)
    {
        await using var db = provider
            .GetRequiredService<IDbConnectionFactory>()
            .CreateConnection();

        await db.OpenAsync();

        await db.ExecuteAsync(
            """
            CREATE TABLE Item
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Nom TEXT NOT NULL UNIQUE,
                Quantite INTEGER NOT NULL DEFAULT 0 CHECK (Quantite >= 0),
                Note TEXT
            );

            CREATE TABLE Etiquette
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ItemId INTEGER NOT NULL REFERENCES Item(Id)
            );
            """);
    }

    public static void DeleteFile(string path)
    {
        // Les connexions restent ouvertes dans le pool : il faut le vider
        // pour libérer le fichier avant de le supprimer.
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Fichier temporaire encore verrouillé : sans conséquence pour le test.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();

        DeleteFile(_path);
    }
}

public class Item
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public int Quantite { get; set; }

    public string? Note { get; set; }
}

public class ItemRepository(IDbConnectionFactory factory, ILogger<ItemRepository> logger)
    : BaseRepository<Item, int>(factory, logger)
{
    private static readonly Dictionary<string, string> Columns = new()
    {
        ["Id"] = "Id",
        ["Nom"] = "Nom",
        ["Quantite"] = "Quantite",
        ["Note"] = "Note"
    };

    protected override IReadOnlyDictionary<string, string> AllowedSortColumns => Columns;

    protected override IReadOnlyDictionary<string, string> AllowedFilterColumns => Columns;

    protected override IReadOnlyList<string> SearchColumns => ["Nom"];

    protected override string DefaultOrderBy => "Quantite DESC";

    protected override string GetByIdSql
        => "SELECT Id, Nom, Quantite, Note FROM Item WHERE Id = @Id";

    protected override string GetAllSql
        => "SELECT Id, Nom, Quantite, Note FROM Item";

    protected override string InsertSql
        => "INSERT INTO Item (Nom, Quantite, Note) VALUES (@Nom, @Quantite, @Note) RETURNING Id";

    protected override string UpdateSql
        => "UPDATE Item SET Nom = @Nom, Quantite = @Quantite, Note = @Note WHERE Id = @Id";

    protected override string DeleteSql
        => "DELETE FROM Item WHERE Id = @Id";

    /// <summary>Requête spécifique passant par ExecuteAsync, comme dans un vrai repository.</summary>
    public Task<int> AddEtiquetteAsync(int itemId)
    {
        return ExecuteAsync(
            (db, transaction) => db.ExecuteAsync(
                "INSERT INTO Etiquette (ItemId) VALUES (@itemId)",
                new { itemId },
                transaction),
            unitOfWork: null,
            CancellationToken.None);
    }

    public Task<int> QueryMissingTableAsync()
    {
        return ExecuteAsync(
            (db, transaction) => db.ExecuteAsync(
                "DELETE FROM TableInexistante",
                transaction: transaction),
            unitOfWork: null,
            CancellationToken.None);
    }
}

/// <summary>
/// Repository sans aucune configuration de pagination.
/// </summary>
public class BareItemRepository(IDbConnectionFactory factory, ILogger<BareItemRepository> logger)
    : BaseRepository<Item, int>(factory, logger)
{
    protected override string GetByIdSql
        => "SELECT Id, Nom, Quantite, Note FROM Item WHERE Id = @Id";

    protected override string GetAllSql
        => "SELECT Id, Nom, Quantite, Note FROM Item";

    protected override string InsertSql
        => "INSERT INTO Item (Nom, Quantite, Note) VALUES (@Nom, @Quantite, @Note) RETURNING Id";

    protected override string UpdateSql
        => "UPDATE Item SET Nom = @Nom, Quantite = @Quantite, Note = @Note WHERE Id = @Id";

    protected override string DeleteSql
        => "DELETE FROM Item WHERE Id = @Id";
}
