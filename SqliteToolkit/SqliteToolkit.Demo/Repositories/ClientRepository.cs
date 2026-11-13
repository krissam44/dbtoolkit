using SqliteToolkit.Abstractions;
using SqliteToolkit.Demo.Models;
using SqliteToolkit.Repositories;

namespace SqliteToolkit.Demo.Repositories;

public class ClientRepository : BaseRepository<Client, int>
{
    public ClientRepository(
        IDbConnectionFactory factory,
        ILogger<ClientRepository> logger)
        : base(factory, logger)
    {
    }

    private static readonly Dictionary<string, string> SortColumns = new()
    {
        ["Id"] = "Id",
        ["Nom"] = "Nom",
        ["Prenom"] = "Prenom",
        ["Email"] = "Email"
    };

    private static readonly Dictionary<string, string> FilterColumns = new()
    {
        ["Id"] = "Id",
        ["Nom"] = "Nom",
        ["Prenom"] = "Prenom",
        ["Email"] = "Email"
    };

    private static readonly string[] SearchedColumns = ["Nom", "Prenom", "Email"];

    protected override IReadOnlyDictionary<string, string> AllowedSortColumns
        => SortColumns;

    protected override IReadOnlyDictionary<string, string> AllowedFilterColumns
        => FilterColumns;

    protected override IReadOnlyList<string> SearchColumns
        => SearchedColumns;

    protected override string DefaultOrderBy => "Nom, Prenom";

    protected override string GetByIdSql => """
        SELECT Id, Nom, Prenom, Email, Telephone
        FROM Client
        WHERE Id = @Id;
        """;

    protected override string GetAllSql => """
        SELECT Id, Nom, Prenom, Email, Telephone
        FROM Client
        """;

    protected override string InsertSql => """
        INSERT INTO Client (Nom, Prenom, Email, Telephone)
        VALUES (@Nom, @Prenom, @Email, @Telephone);
        SELECT last_insert_rowid();
        """;

    protected override string UpdateSql => """
        UPDATE Client
        SET Nom = @Nom,
            Prenom = @Prenom,
            Email = @Email,
            Telephone = @Telephone
        WHERE Id = @Id;
        """;

    protected override string DeleteSql => """
        DELETE FROM Client
        WHERE Id = @Id;
        """;
}
