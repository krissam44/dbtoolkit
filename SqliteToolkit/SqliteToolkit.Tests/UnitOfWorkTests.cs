using SqliteToolkit.Exceptions;

namespace SqliteToolkit.Tests;

public class UnitOfWorkTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await TestDatabase.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task<int> CountAsync() => (await _db.Items.GetAllAsync()).Count;

    [Fact]
    public async Task Commit_enregistre_toutes_les_operations()
    {
        await using (var uow = await _db.UnitOfWork.BeginAsync())
        {
            await _db.Items.InsertAsync(new Item { Nom = "a" }, uow);
            await _db.Items.InsertAsync(new Item { Nom = "b" }, uow);
            await uow.CommitAsync();
        }

        Assert.Equal(2, await CountAsync());
    }

    [Fact]
    public async Task Rollback_explicite_annule_tout()
    {
        await using (var uow = await _db.UnitOfWork.BeginAsync())
        {
            await _db.Items.InsertAsync(new Item { Nom = "a" }, uow);
            await uow.RollbackAsync();
        }

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Dispose_sans_commit_annule_tout()
    {
        await using (var uow = await _db.UnitOfWork.BeginAsync())
        {
            await _db.Items.InsertAsync(new Item { Nom = "a" }, uow);
        }

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Erreur_en_cours_de_transaction_n_enregistre_rien()
    {
        await Assert.ThrowsAsync<DataAccessException>(async () =>
        {
            await using var uow = await _db.UnitOfWork.BeginAsync();
            await _db.Items.InsertAsync(new Item { Nom = "a" }, uow);
            await _db.Items.InsertAsync(new Item { Nom = "a" }, uow); // Nom est UNIQUE
            await uow.CommitAsync();
        });

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Lecture_dans_la_transaction_voit_les_lignes_non_validees()
    {
        await using var uow = await _db.UnitOfWork.BeginAsync();

        var id = await _db.Items.InsertAsync(new Item { Nom = "a" }, uow);

        Assert.Single(await _db.Items.GetAllAsync(uow));
        Assert.Equal("a", (await _db.Items.GetByIdAsync(id, uow))?.Nom);
    }

    [Fact]
    public async Task Utilisation_apres_commit_est_refusee()
    {
        await using var uow = await _db.UnitOfWork.BeginAsync();
        await uow.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _db.Items.InsertAsync(new Item { Nom = "a" }, uow));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => uow.CommitAsync());

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Utilisation_apres_dispose_est_refusee()
    {
        var uow = await _db.UnitOfWork.BeginAsync();
        await uow.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _db.Items.InsertAsync(new Item { Nom = "a" }, uow));
    }

    [Fact]
    public async Task Plusieurs_transactions_successives_fonctionnent()
    {
        for (var i = 0; i < 3; i++)
        {
            await using var uow = await _db.UnitOfWork.BeginAsync();
            await _db.Items.InsertAsync(new Item { Nom = $"n{i}" }, uow);
            await uow.CommitAsync();
        }

        Assert.Equal(3, await CountAsync());
    }

    [Fact]
    public async Task Sans_unit_of_work_le_crud_fonctionne()
    {
        var id = await _db.Items.InsertAsync(new Item { Nom = "a", Quantite = 1 });

        Assert.True(await _db.Items.UpdateAsync(new Item { Id = id, Nom = "b", Quantite = 2 }));

        var item = await _db.Items.GetByIdAsync(id);
        Assert.Equal("b", item?.Nom);
        Assert.Equal(2, item?.Quantite);

        Assert.True(await _db.Items.DeleteAsync(id));
        Assert.False(await _db.Items.DeleteAsync(id));
        Assert.Null(await _db.Items.GetByIdAsync(id));
    }

    [Fact]
    public async Task Erreur_sqlite_devient_DataAccessException()
    {
        await _db.Items.InsertAsync(new Item { Nom = "a" });

        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => _db.Items.InsertAsync(new Item { Nom = "a" }));

        Assert.IsType<Microsoft.Data.Sqlite.SqliteException>(ex.InnerException);
    }

    [Fact]
    public async Task Annulation_reste_une_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _db.Items.GetByIdAsync(1, cancellationToken: cts.Token));
    }
}
