using Microsoft.Extensions.Logging;
using SqliteToolkit.Abstractions;
using SqliteToolkit.Repositories;

namespace SqliteToolkit.Demo.Repositories;

public class ErrorTestRepository : BaseRepository<TestEntity, int>
{
    public ErrorTestRepository(
        IDbConnectionFactory factory,
        ILogger<ErrorTestRepository> logger)
        : base(factory, logger)
    {
    }

    protected override string GetByIdSql =>
        "SELECT * FROM TableInexistante WHERE Id = @Id";

    protected override string GetAllSql =>
        "SELECT * FROM TableInexistante";

    protected override string InsertSql =>
        """INSERT INTO Client (Nom) VALUES (@Nom) RETURNING Id;""";

    protected override string UpdateSql =>
        "UPDATE TableInexistante SET Id = @Id WHERE Id = @Id";

    protected override string DeleteSql =>
        "DELETE FROM TableInexistante WHERE Id = @Id";
}

public class TestEntity
{
    public int Id { get; set; }
    public string? Nom { get; set; }
}