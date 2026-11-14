using DbToolkit.Exceptions;

namespace DbToolkit.Tests;

public abstract class DataAccessErrorKindTests : ProviderTests
{
    [Fact]
    public async Task Doublon_est_une_UniqueViolation()
    {
        await Db.Items.InsertAsync(new Item { Nom = "a" });

        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Items.InsertAsync(new Item { Nom = "a" }));

        Assert.Equal(DataAccessErrorKind.UniqueViolation, ex.Kind);
    }

    [Fact]
    public async Task Valeur_obligatoire_absente_est_une_NotNullViolation()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Items.InsertAsync(new Item { Nom = null! }));

        Assert.Equal(DataAccessErrorKind.NotNullViolation, ex.Kind);
    }

    [Fact]
    public async Task Regle_check_non_respectee_est_une_CheckViolation()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Items.InsertAsync(new Item { Nom = "a", Quantite = -1 }));

        Assert.Equal(DataAccessErrorKind.CheckViolation, ex.Kind);
    }

    [Fact]
    public async Task Reference_inexistante_est_une_ForeignKeyViolation()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Items.AddEtiquetteAsync(itemId: 999));

        Assert.Equal(DataAccessErrorKind.ForeignKeyViolation, ex.Kind);
    }

    [Fact]
    public async Task Suppression_d_une_ligne_encore_referencee_est_une_ForeignKeyViolation()
    {
        var id = await Db.Items.InsertAsync(new Item { Nom = "a" });
        await Db.Items.AddEtiquetteAsync(id);

        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Items.DeleteAsync(id));

        Assert.Equal(DataAccessErrorKind.ForeignKeyViolation, ex.Kind);
    }

    [Fact]
    public async Task Erreur_de_syntaxe_reste_de_nature_inconnue()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Items.QueryMissingTableAsync());

        Assert.Equal(DataAccessErrorKind.Unknown, ex.Kind);
    }
}

public class SqliteDataAccessErrorKindTests : DataAccessErrorKindTests
{
    protected override async Task<TestDatabase?> CreateDatabaseAsync()
        => await TestDatabase.CreateSqliteAsync();
}

public class MariaDbDataAccessErrorKindTests : DataAccessErrorKindTests
{
    protected override Task<TestDatabase?> CreateDatabaseAsync()
        => TestDatabase.TryCreateMariaDbAsync();
}
