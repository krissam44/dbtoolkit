using SqliteToolkit.Exceptions;

namespace SqliteToolkit.Tests;

public class DataAccessErrorKindTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Doublon_est_une_UniqueViolation()
    {
        await _db.Items.InsertAsync(new Item { Nom = "a" });

        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => _db.Items.InsertAsync(new Item { Nom = "a" }));

        Assert.Equal(DataAccessErrorKind.UniqueViolation, ex.Kind);
    }

    [Fact]
    public async Task Valeur_obligatoire_absente_est_une_NotNullViolation()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => _db.Items.InsertAsync(new Item { Nom = null! }));

        Assert.Equal(DataAccessErrorKind.NotNullViolation, ex.Kind);
    }

    [Fact]
    public async Task Regle_check_non_respectee_est_une_CheckViolation()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => _db.Items.InsertAsync(new Item { Nom = "a", Quantite = -1 }));

        Assert.Equal(DataAccessErrorKind.CheckViolation, ex.Kind);
    }

    [Fact]
    public async Task Reference_inexistante_est_une_ForeignKeyViolation()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => _db.Items.AddEtiquetteAsync(itemId: 999));

        Assert.Equal(DataAccessErrorKind.ForeignKeyViolation, ex.Kind);
    }

    [Fact]
    public async Task Suppression_d_une_ligne_encore_referencee_est_une_ForeignKeyViolation()
    {
        var id = await _db.Items.InsertAsync(new Item { Nom = "a" });
        await _db.Items.AddEtiquetteAsync(id);

        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => _db.Items.DeleteAsync(id));

        Assert.Equal(DataAccessErrorKind.ForeignKeyViolation, ex.Kind);
    }

    [Fact]
    public async Task Erreur_de_syntaxe_reste_de_nature_inconnue()
    {
        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => _db.Items.QueryMissingTableAsync());

        Assert.Equal(DataAccessErrorKind.Unknown, ex.Kind);
    }
}
