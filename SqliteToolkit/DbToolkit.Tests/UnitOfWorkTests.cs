using DbToolkit.Exceptions;

namespace DbToolkit.Tests;

public abstract class UnitOfWorkTests : ProviderTests
{
    private async Task<int> CountAsync() => (await Db.Items.GetAllAsync()).Count;

    [Fact]
    public async Task Commit_enregistre_toutes_les_operations()
    {
        await using (var uow = await Db.UnitOfWork.BeginAsync())
        {
            await Db.Items.InsertAsync(new Item { Nom = "a" }, uow);
            await Db.Items.InsertAsync(new Item { Nom = "b" }, uow);
            await uow.CommitAsync();
        }

        Assert.Equal(2, await CountAsync());
    }

    [Fact]
    public async Task Rollback_explicite_annule_tout()
    {
        await using (var uow = await Db.UnitOfWork.BeginAsync())
        {
            await Db.Items.InsertAsync(new Item { Nom = "a" }, uow);
            await uow.RollbackAsync();
        }

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Dispose_sans_commit_annule_tout()
    {
        await using (var uow = await Db.UnitOfWork.BeginAsync())
        {
            await Db.Items.InsertAsync(new Item { Nom = "a" }, uow);
        }

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Erreur_en_cours_de_transaction_n_enregistre_rien()
    {
        await Assert.ThrowsAsync<DataAccessException>(async () =>
        {
            await using var uow = await Db.UnitOfWork.BeginAsync();
            await Db.Items.InsertAsync(new Item { Nom = "a" }, uow);
            await Db.Items.InsertAsync(new Item { Nom = "a" }, uow); // Nom est UNIQUE
            await uow.CommitAsync();
        });

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Lecture_dans_la_transaction_voit_les_lignes_non_validees()
    {
        await using var uow = await Db.UnitOfWork.BeginAsync();

        var id = await Db.Items.InsertAsync(new Item { Nom = "a" }, uow);

        Assert.Single(await Db.Items.GetAllAsync(uow));
        Assert.Equal("a", (await Db.Items.GetByIdAsync(id, uow))?.Nom);
    }

    [Fact]
    public async Task Utilisation_apres_commit_est_refusee()
    {
        await using var uow = await Db.UnitOfWork.BeginAsync();
        await uow.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Db.Items.InsertAsync(new Item { Nom = "a" }, uow));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => uow.CommitAsync());

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Utilisation_apres_dispose_est_refusee()
    {
        var uow = await Db.UnitOfWork.BeginAsync();
        await uow.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => Db.Items.InsertAsync(new Item { Nom = "a" }, uow));
    }

    [Fact]
    public async Task Plusieurs_transactions_successives_fonctionnent()
    {
        for (var i = 0; i < 3; i++)
        {
            await using var uow = await Db.UnitOfWork.BeginAsync();
            await Db.Items.InsertAsync(new Item { Nom = $"n{i}" }, uow);
            await uow.CommitAsync();
        }

        Assert.Equal(3, await CountAsync());
    }

    [Fact]
    public async Task Sans_unit_of_work_le_crud_fonctionne()
    {
        var id = await Db.Items.InsertAsync(new Item { Nom = "a", Quantite = 1 });

        Assert.True(await Db.Items.UpdateAsync(new Item { Id = id, Nom = "b", Quantite = 2 }));

        var item = await Db.Items.GetByIdAsync(id);
        Assert.Equal("b", item?.Nom);
        Assert.Equal(2, item?.Quantite);

        Assert.True(await Db.Items.DeleteAsync(id));
        Assert.False(await Db.Items.DeleteAsync(id));
        Assert.Null(await Db.Items.GetByIdAsync(id));
    }

    [Fact]
    public async Task Erreur_sqlite_devient_DataAccessException()
    {
        await Db.Items.InsertAsync(new Item { Nom = "a" });

        var ex = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Items.InsertAsync(new Item { Nom = "a" }));

        Assert.IsAssignableFrom<System.Data.Common.DbException>(ex.InnerException);
    }

    [Fact]
    public async Task Annulation_reste_une_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Db.Items.GetByIdAsync(1, cancellationToken: cts.Token));
    }
}

public class SqliteUnitOfWorkTests : UnitOfWorkTests
{
    protected override async Task<TestDatabase?> CreateDatabaseAsync()
        => await TestDatabase.CreateSqliteAsync();
}

public class MariaDbUnitOfWorkTests : UnitOfWorkTests
{
    protected override Task<TestDatabase?> CreateDatabaseAsync()
        => TestDatabase.TryCreateMariaDbAsync();
}
