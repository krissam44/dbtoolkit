# DbToolkit

Accès aux données **Dapper** pour **.NET 10**, avec **SQLite** et **MariaDB** derrière la même API : repositories, pagination unifiée, transactions, migrations SQL, contrôle de version des lignes et reprise sur interblocage. Des adaptateurs relient la pagination aux grilles **MudBlazor** et **Syncfusion** et aux minimal APIs ASP.NET Core.

L'idée : écrire son SQL à la main, une fois, et ne pas réécrire à chaque projet la pagination, les tris, les filtres, la gestion des erreurs de la base ni les transactions.

> Les commentaires et messages de la bibliothèque sont en français.

## Paquets

| Paquet | Rôle |
| --- | --- |
| `DbToolkit` | Cœur neutre : `BaseRepository`, `PageRequest` / `PagedResult`, `IUnitOfWorkFactory`, `MigrationRunner`, `DataAccessException`, `RetryPolicy`. |
| `SqliteToolkit` | Fournisseur SQLite (`Microsoft.Data.Sqlite`) : `services.AddSqliteToolkit(...)`. |
| `MariaDbToolkit` | Fournisseur MariaDB / MySQL (`MySqlConnector`) : `services.AddMariaDbToolkit(...)`. |
| `DbToolkit.Adapters.AspNetCore` | `PageQuery` pour les minimal APIs, erreurs de base traduites en `ProblemDetails`. |
| `DbToolkit.Adapters.MudBlazor` | `MudDataGrid` en chargement serveur (`GridState` → `PageRequest`). |
| `DbToolkit.Adapters.Syncfusion` | `SfGrid` avec `DataAdaptor` personnalisé (`DataManagerRequest` → `PageRequest`). |

`DbToolkit.Demo` est une application Blazor complète qui utilise le tout (page MudBlazor, page Syncfusion, endpoints `/api/clients`).

## Installation

Les paquets sont publiés sur **GitHub Packages** (`https://nuget.pkg.github.com/krissam44/index.json`). Cette source demande une authentification, même pour lire : créez un jeton classique avec le droit `read:packages`, puis enregistrez la source **dans votre configuration utilisateur** (jamais dans un dépôt) :

```bash
dotnet nuget add source https://nuget.pkg.github.com/krissam44/index.json --name github-krissam44 --username VOTRE_NOM --password VOTRE_JETON --store-password-in-clear-text
```

Puis, par exemple pour une application MariaDB :

```bash
dotnet add package MariaDbToolkit
dotnet add package DbToolkit.Adapters.AspNetCore
```

Autres possibilités : `dotnet pack src/DbToolkit.slnx -c Release` produit les paquets dans `src/nupkg` (à déclarer comme source locale dans un `nuget.config`), ou une référence de projet vers les `.csproj` de ce dépôt.

## Démarrage rapide

```csharp
// MariaDB
services.AddMariaDbToolkit(o => o.ConnectionString = configuration.GetConnectionString("Db")!);

// ou SQLite
services.AddSqliteToolkit(o => o.ConnectionString = "Data Source=app.db");

services.AddSingleton<ClientRepository>();
```

Un repository fournit son SQL ; `BaseRepository` s'occupe du reste :

```csharp
public class ClientRepository(IDbConnectionFactory factory, ILogger<ClientRepository> logger)
    : BaseRepository<Client, int>(factory, logger)
{
    // Listes blanches : seules ces colonnes peuvent être triées, filtrées, recherchées.
    private static readonly Dictionary<string, string> Colonnes = new()
    {
        ["Nom"] = "nom", ["Email"] = "email"
    };

    protected override IReadOnlyDictionary<string, string> AllowedSortColumns => Colonnes;
    protected override IReadOnlyDictionary<string, string> AllowedFilterColumns => Colonnes;
    protected override IReadOnlyList<string> SearchColumns => ["nom", "email"];
    protected override string DefaultOrderBy => "nom";

    protected override string GetAllSql => "SELECT id, nom, email FROM client";   // sans ORDER BY
    protected override string GetByIdSql => GetAllSql + " WHERE id = @Id";
    protected override string InsertSql => "INSERT INTO client (nom, email) VALUES (@Nom, @Email) RETURNING id";
    protected override string UpdateSql => "UPDATE client SET nom = @Nom, email = @Email WHERE id = @Id";
    protected override string DeleteSql => "DELETE FROM client WHERE id = @Id";
}
```

## Pagination unifiée

Une seule demande, quelle que soit l'interface (grille, API, code) :

```csharp
var page = await clients.GetPagedAsync(new PageRequest
{
    Skip = 0,
    Take = 20,
    Sorts = [new SortCriteria("Nom")],
    Filters = [new FilterCriteria("Email", FilterOperator.EndsWith, ".fr")],
    Search = "dupont"
});

// page.Items, page.TotalCount, page.TotalPages...
```

Les noms de colonnes sont des noms logiques : un nom hors liste blanche est refusé (`ArgumentException`), jamais injecté dans le SQL. Les valeurs sont toujours des paramètres.

`PageRequestLimits` borne les demandes venues de l'extérieur (taille de page, nombre de filtres, longueur de la recherche).

## Transactions

```csharp
await unitOfWorkFactory.ExecuteInTransactionAsync(async uow =>
{
    ligne.CommandeId = await commandes.InsertAsync(commande, uow);
    await lignes.InsertAsync(ligne, uow);
});
```

Chaque méthode de repository accepte un `IUnitOfWork` optionnel : fourni, elle s'exécute dans sa transaction ; absent, elle ouvre et ferme sa propre connexion. Libérée sans validation, une transaction est annulée.

## Accès concurrents

**Contrôle de version.** La table porte une colonne `version`, que `UpdateSql` vérifie et incrémente :

```sql
UPDATE client SET nom = @Nom, version = version + 1 WHERE id = @Id AND version = @Version
```

Le repository ajoute `ConcurrencyCheckSql => "SELECT 1 FROM client WHERE id = @Id"`. `UpdateAsync` lève alors `ConcurrencyException` quand la ligne a été modifiée par un autre utilisateur, et renvoie `false` quand elle a disparu. `DeleteVersionedAsync` fait de même pour la suppression.

**Reprise sur verrou ou interblocage.** `ExecuteInTransactionAsync` rejoue la transaction entière quand le moteur signale une attente de verrou expirée ou un interblocage (`RetryPolicy`, trois reprises avec attente exponentielle par défaut). Le travail passé à `ExecuteInTransactionAsync` peut donc s'exécuter plusieurs fois : il ne doit rien faire d'irréversible hors de la base. Hors transaction, `BaseRepository` reprend seul l'opération.

## Erreurs

Les erreurs du moteur deviennent des `DataAccessException` dont `Kind` ne dépend pas de la base : `UniqueViolation`, `ForeignKeyViolation`, `NotNullViolation`, `CheckViolation`, `Busy`, `Deadlock`, `ConcurrencyConflict`. `IsTransient` indique si l'opération mérite d'être retentée.

## Migrations

Des scripts SQL embarqués, nommés `NNN_Nom.sql`, appliqués dans l'ordre et tracés dans une table `SchemaMigrations` :

```xml
<EmbeddedResource Include="Migrations\Sql\*.sql" />
```

```csharp
await scope.ServiceProvider.GetRequiredService<MigrationRunner>()
    .MigrateAsync(typeof(Program).Assembly, "MonAppli.Migrations.Sql");
```

⚠ Sur MariaDB et MySQL, `CREATE`, `ALTER` et `DROP` valident implicitement la transaction : un script qui échoue à mi-chemin n'est pas annulé. Écrivez les migrations pour qu'elles soient rejouables (`IF NOT EXISTS`, `INSERT IGNORE`).

## Adaptateurs

```csharp
// MudBlazor : <MudDataGrid ServerData="...">
ServerData = async state => (await clients.GetPagedAsync(state.ToPageRequest(termeRecherche))).ToGridData();

// Syncfusion : DataAdaptor personnalisé
public override async Task<object> ReadAsync(DataManagerRequest request, string? key = null)
    => (await clients.GetPagedAsync(request.ToPageRequest())).ToDataResult(request);

// Minimal API
services.AddDbToolkitApi();                       // bornes de page
var api = app.MapGroup("/api/clients").WithDbToolkitProblems();   // erreurs → ProblemDetails
api.MapGet("/", ([AsParameters] PageQuery q, PageRequestLimits limits, ClientRepository r) =>
    r.GetPagedAsync(q.ToPageRequest(limits)));
// GET /api/clients?page=2&pageSize=20&search=dupont&sort=Nom,-Id&filter=Email:endswith:.fr
```

## Tests

La suite (xUnit v3, plus de 200 tests) s'exécute à l'identique sur SQLite et MariaDB. Pour MariaDB, un serveur jetable :

```bash
docker compose -f src/docker-compose.tests.yml up -d
dotnet test --project src/DbToolkit.Tests/DbToolkit.Tests.csproj
```

Le serveur écoute sur le port 13317 (`DBTOOLKIT_MARIADB_TESTS` permet d'en changer). S'il est injoignable, les tests MariaDB sont ignorés, pas échoués : vérifiez le compteur « ignoré » du résumé.

## Limites connues

- Les filtres combinés par **OU** sont refusés par l'adaptateur Syncfusion (le filtre à cases multiples de type Excel ne fonctionne pas) ; utilisez les filtres par menu.
- Les bornes de `PageRequestLimits` s'appliquent à l'API, pas aux grilles.
- `BaseRepository` impose cinq requêtes même à une liste en lecture seule.
- La reprise sur interblocage est testée sur MariaDB avec un vrai interblocage ; sur SQLite, les erreurs de verrou sont simulées.
- Les adaptateurs Syncfusion supposent une licence Syncfusion valide (leur clé se lit dans la configuration, jamais dans le dépôt).

## Licence

[MIT](LICENSE).
