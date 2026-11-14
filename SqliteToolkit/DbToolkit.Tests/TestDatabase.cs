using Dapper;
using DbToolkit.Abstractions;
using DbToolkit.Repositories;
using MariaDbToolkit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SqliteToolkit;

namespace DbToolkit.Tests;

/// <summary>
/// Base jetable, propre à chaque test : un fichier temporaire pour SQLite,
/// une base créée puis supprimée sur le serveur de test pour MariaDB.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>
    /// Connexion d'administration du serveur MariaDB de test. Par défaut, le conteneur
    /// de <c>docker-compose.tests.yml</c> ; remplaçable par la variable d'environnement
    /// <c>DBTOOLKIT_MARIADB_TESTS</c>.
    /// </summary>
    private static readonly string MariaDbAdminConnectionString =
        Environment.GetEnvironmentVariable("DBTOOLKIT_MARIADB_TESTS")
        ?? "Server=127.0.0.1;Port=3317;User ID=root;Password=dbtoolkit_tests;Connection Timeout=3;Pooling=false";

    private static readonly Lazy<Task<bool>> MariaDbAvailable = new(ProbeMariaDbAsync);

    public const string MariaDbUnavailableMessage =
        "Serveur MariaDB de test injoignable. Démarrez-le avec : " +
        "docker compose -f docker-compose.tests.yml up -d";

    private readonly ServiceProvider _provider;
    private readonly Func<Task> _cleanup;

    private TestDatabase(ServiceProvider provider, Func<Task> cleanup)
    {
        _provider = provider;
        _cleanup = cleanup;
    }

    public IUnitOfWorkFactory UnitOfWork
        => _provider.GetRequiredService<IUnitOfWorkFactory>();

    public ItemRepository Items
        => _provider.GetRequiredService<ItemRepository>();

    public BareItemRepository BareItems
        => _provider.GetRequiredService<BareItemRepository>();

    // ---------------------------------------------------------------------------------
    // SQLite

    public static async Task<TestDatabase> CreateSqliteAsync()
    {
        var path = NewSqlitePath();

        var services = new ServiceCollection();
        services.AddLogging();
        ConfigureSqliteServices(services, path);

        var provider = services.BuildServiceProvider();

        await CreateSqliteSchemaAsync(provider);

        return new TestDatabase(
            provider,
            () =>
            {
                DeleteSqliteFile(path);
                return Task.CompletedTask;
            });
    }

    public static string NewSqlitePath()
        => Path.Combine(
            Path.GetTempPath(),
            $"dbtoolkit_tests_{Guid.NewGuid():N}.db");

    public static void ConfigureSqliteServices(IServiceCollection services, string path)
    {
        services.AddSqliteToolkit(options => options.ConnectionString = $"Data Source={path}");
        services.AddSingleton<ItemRepository>();
        services.AddSingleton<BareItemRepository>();
    }

    public static async Task CreateSqliteSchemaAsync(IServiceProvider provider)
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

    public static void DeleteSqliteFile(string path)
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

    // ---------------------------------------------------------------------------------
    // MariaDB

    /// <summary>
    /// Crée une base dédiée sur le serveur de test, ou renvoie <c>null</c>
    /// si le serveur est injoignable (le test est alors ignoré).
    /// </summary>
    public static async Task<TestDatabase?> TryCreateMariaDbAsync()
    {
        if (!await MariaDbAvailable.Value)
            return null;

        var name = $"dbtoolkit_t_{Guid.NewGuid():N}";

        await using (var admin = new MySqlConnection(MariaDbAdminConnectionString))
        {
            await admin.OpenAsync();
            await admin.ExecuteAsync($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4;");
        }

        // Sans pool : chaque test a sa propre base, donc sa propre chaîne de
        // connexion ; des pools par test épuiseraient les connexions du serveur.
        var connectionString = new MySqlConnectionStringBuilder(MariaDbAdminConnectionString)
        {
            Database = name,
            Pooling = false
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMariaDbToolkit(options => options.ConnectionString = connectionString);
        services.AddSingleton<ItemRepository>();
        services.AddSingleton<BareItemRepository>();

        var provider = services.BuildServiceProvider();

        await using (var db = provider.GetRequiredService<IDbConnectionFactory>().CreateConnection())
        {
            await db.OpenAsync();

            await db.ExecuteAsync(
                """
                CREATE TABLE Item
                (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    Nom VARCHAR(100) NOT NULL UNIQUE,
                    Quantite INT NOT NULL DEFAULT 0 CHECK (Quantite >= 0),
                    Note VARCHAR(200) NULL
                ) ENGINE = InnoDB;

                CREATE TABLE Etiquette
                (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    ItemId INT NOT NULL,
                    CONSTRAINT FK_Etiquette_Item FOREIGN KEY (ItemId) REFERENCES Item(Id)
                ) ENGINE = InnoDB;
                """);
        }

        return new TestDatabase(
            provider,
            async () =>
            {
                await using var admin = new MySqlConnection(MariaDbAdminConnectionString);
                await admin.OpenAsync();
                await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{name}`;");
            });
    }

    private static async Task<bool> ProbeMariaDbAsync()
    {
        try
        {
            await using var admin = new MySqlConnection(MariaDbAdminConnectionString);
            await admin.OpenAsync();
            return true;
        }
        catch (MySqlException)
        {
            return false;
        }
    }

    // ---------------------------------------------------------------------------------

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _cleanup();
    }
}

/// <summary>
/// Base des tests exécutés à l'identique sur chaque moteur : la classe dérivée
/// choisit la base, et le test est ignoré si elle est indisponible.
/// </summary>
public abstract class ProviderTests : IAsyncLifetime
{
    private TestDatabase? _db;

    protected abstract Task<TestDatabase?> CreateDatabaseAsync();

    protected virtual Task SeedAsync() => Task.CompletedTask;

    protected TestDatabase Db
    {
        get
        {
            if (_db is null)
                Assert.Skip(TestDatabase.MariaDbUnavailableMessage);

            return _db;
        }
    }

    public async ValueTask InitializeAsync()
    {
        _db = await CreateDatabaseAsync();

        if (_db is not null)
            await SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
            await _db.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}

public class Item
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public int Quantite { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Le même repository, avec le même SQL, sert aux deux moteurs.
/// </summary>
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
