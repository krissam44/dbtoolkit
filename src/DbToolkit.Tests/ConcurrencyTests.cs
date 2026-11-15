using DbToolkit.Abstractions;
using DbToolkit.Exceptions;
using DbToolkit.Models;
using Microsoft.Data.Sqlite;

namespace DbToolkit.Tests;

/// <summary>
/// Écritures gardées par un numéro de version : deux utilisateurs
/// qui modifient la même ligne ne s'écrasent pas.
/// </summary>
public abstract class VersionTests : ProviderTests
{
    [Fact]
    public async Task Une_modification_incremente_la_version()
    {
        var id = await Db.Documents.InsertAsync(new Document { Titre = "v1" });
        var document = (await Db.Documents.GetByIdAsync(id))!;

        document.Titre = "v2";

        Assert.True(await Db.Documents.UpdateAsync(document));

        var relu = (await Db.Documents.GetByIdAsync(id))!;
        Assert.Equal("v2", relu.Titre);
        Assert.Equal(2, relu.Version);
    }

    [Fact]
    public async Task La_seconde_de_deux_modifications_concurrentes_est_refusee()
    {
        var id = await Db.Documents.InsertAsync(new Document { Titre = "origine" });

        var lectureA = (await Db.Documents.GetByIdAsync(id))!;
        var lectureB = (await Db.Documents.GetByIdAsync(id))!;

        lectureA.Titre = "modifié par A";
        await Db.Documents.UpdateAsync(lectureA);

        lectureB.Titre = "modifié par B";
        var erreur = await Assert.ThrowsAsync<ConcurrencyException>(
            () => Db.Documents.UpdateAsync(lectureB));

        Assert.Equal(DataAccessErrorKind.ConcurrencyConflict, erreur.Kind);
        Assert.False(erreur.IsTransient);
        Assert.Equal("modifié par A", (await Db.Documents.GetByIdAsync(id))!.Titre);
    }

    [Fact]
    public async Task Modifier_une_ligne_disparue_renvoie_false_sans_exception()
    {
        var id = await Db.Documents.InsertAsync(new Document { Titre = "a" });
        var document = (await Db.Documents.GetByIdAsync(id))!;

        await Db.Documents.DeleteAsync(id);

        Assert.False(await Db.Documents.UpdateAsync(document));
    }

    [Fact]
    public async Task Sans_requete_de_controle_un_conflit_renvoie_false()
    {
        var id = await Db.UnguardedDocuments.InsertAsync(new Document { Titre = "origine" });

        var lectureA = (await Db.UnguardedDocuments.GetByIdAsync(id))!;
        var lectureB = (await Db.UnguardedDocuments.GetByIdAsync(id))!;

        lectureA.Titre = "A";
        Assert.True(await Db.UnguardedDocuments.UpdateAsync(lectureA));

        lectureB.Titre = "B";
        Assert.False(await Db.UnguardedDocuments.UpdateAsync(lectureB));
        Assert.Equal("A", (await Db.UnguardedDocuments.GetByIdAsync(id))!.Titre);
    }

    [Fact]
    public async Task Une_suppression_gardee_refuse_une_ligne_modifiee_entre_temps()
    {
        var id = await Db.Documents.InsertAsync(new Document { Titre = "origine" });

        var lecture = (await Db.Documents.GetByIdAsync(id))!;
        var autre = (await Db.Documents.GetByIdAsync(id))!;

        autre.Titre = "modifié";
        await Db.Documents.UpdateAsync(autre);

        await Assert.ThrowsAsync<ConcurrencyException>(
            () => Db.Documents.DeleteVersionedAsync(lecture));

        Assert.NotNull(await Db.Documents.GetByIdAsync(id));

        // Relue, la ligne se supprime ; une seconde tentative ne trouve plus rien.
        var relue = (await Db.Documents.GetByIdAsync(id))!;
        Assert.True(await Db.Documents.DeleteVersionedAsync(relue));
        Assert.False(await Db.Documents.DeleteVersionedAsync(relue));
    }

    [Fact]
    public async Task La_suppression_gardee_exige_sa_requete()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => Db.UnguardedDocuments.DeleteVersionedAsync(new Document { Id = 1 }));
    }

    [Fact]
    public async Task Un_conflit_dans_une_transaction_annule_tout_et_n_est_pas_repris()
    {
        var id = await Db.Documents.InsertAsync(new Document { Titre = "origine" });
        var perimee = (await Db.Documents.GetByIdAsync(id))!;

        var fraiche = (await Db.Documents.GetByIdAsync(id))!;
        fraiche.Titre = "à jour";
        await Db.Documents.UpdateAsync(fraiche);

        var essais = 0;

        await Assert.ThrowsAsync<ConcurrencyException>(
            () => Db.UnitOfWork.ExecuteInTransactionAsync(async uow =>
            {
                essais++;
                await Db.Documents.InsertAsync(new Document { Titre = "ne doit pas rester" }, uow);
                await Db.Documents.UpdateAsync(perimee, uow);
            }));

        Assert.Equal(1, essais);
        Assert.Single(await Db.Documents.GetAllAsync());
    }
}

public class SqliteVersionTests : VersionTests
{
    protected override async Task<TestDatabase?> CreateDatabaseAsync()
        => await TestDatabase.CreateSqliteAsync();
}

public class MariaDbVersionTests : VersionTests
{
    protected override Task<TestDatabase?> CreateDatabaseAsync()
        => TestDatabase.TryCreateMariaDbAsync();
}

/// <summary>
/// Reprise après une erreur passagère. Le verrou est simulé par une erreur
/// SQLITE_BUSY, que le dialecte classe comme passagère.
/// </summary>
public class RetryTests : ProviderTests
{
    private static readonly RetryPolicy Rapide = new() { MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(1) };

    protected override async Task<TestDatabase?> CreateDatabaseAsync()
        => await TestDatabase.CreateSqliteAsync();

    private static SqliteException BaseOccupee() => new("database is locked", 5);

    [Fact]
    public async Task Une_transaction_bloquee_est_reprise_jusqu_a_reussir()
    {
        var essais = 0;

        var id = await Db.UnitOfWork.ExecuteInTransactionAsync(
            async uow =>
            {
                essais++;

                var nouveau = await Db.Documents.InsertAsync(new Document { Titre = $"essai {essais}" }, uow);

                if (essais < 3)
                    throw BaseOccupee();

                return nouveau;
            },
            Rapide);

        Assert.Equal(3, essais);

        // Les deux premiers essais ont été annulés : une seule ligne, celle du troisième.
        var documents = await Db.Documents.GetAllAsync();
        Assert.Equal("essai 3", Assert.Single(documents).Titre);
        Assert.Equal(id, documents[0].Id);
    }

    [Fact]
    public async Task Au_dela_du_nombre_de_reprises_l_erreur_remonte()
    {
        var essais = 0;

        await Assert.ThrowsAsync<SqliteException>(
            () => Db.UnitOfWork.ExecuteInTransactionAsync(
                uow =>
                {
                    essais++;
                    throw BaseOccupee();
                },
                Rapide));

        Assert.Equal(4, essais); // le premier essai et trois reprises
    }

    [Fact]
    public async Task Une_erreur_non_passagere_n_est_pas_reprise()
    {
        await Db.Items.InsertAsync(new Item { Nom = "a" });

        var essais = 0;

        var erreur = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.UnitOfWork.ExecuteInTransactionAsync(
                async uow =>
                {
                    essais++;
                    await Db.Items.InsertAsync(new Item { Nom = "a" }, uow); // doublon
                },
                Rapide));

        Assert.Equal(DataAccessErrorKind.UniqueViolation, erreur.Kind);
        Assert.Equal(1, essais);
    }

    [Fact]
    public async Task Sans_politique_de_reprise_la_premiere_erreur_remonte()
    {
        var essais = 0;

        await Assert.ThrowsAsync<SqliteException>(
            () => Db.UnitOfWork.ExecuteInTransactionAsync(
                uow =>
                {
                    essais++;
                    throw BaseOccupee();
                },
                RetryPolicy.None));

        Assert.Equal(1, essais);
    }

    [Fact]
    public async Task Hors_transaction_le_repository_reprend_l_operation()
    {
        var essais = 0;

        var resultat = await Db.Documents.RunAsync(() =>
        {
            essais++;

            return essais < 3
                ? throw BaseOccupee()
                : Task.FromResult(42);
        });

        Assert.Equal(42, resultat);
        Assert.Equal(3, essais);
    }

    [Fact]
    public async Task Dans_une_transaction_le_repository_ne_reprend_pas_seul()
    {
        var essais = 0;

        await using var uow = await Db.UnitOfWork.BeginAsync();

        var erreur = await Assert.ThrowsAsync<DataAccessException>(
            () => Db.Documents.RunAsync(
                () =>
                {
                    essais++;
                    throw BaseOccupee();
                },
                uow));

        // C'est toute la transaction qu'il faudrait rejouer, pas une instruction.
        Assert.Equal(DataAccessErrorKind.Busy, erreur.Kind);
        Assert.True(erreur.IsTransient);
        Assert.Equal(1, essais);
    }

    [Fact]
    public void L_attente_double_a_chaque_reprise()
    {
        var policy = new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(100) };

        Assert.InRange(policy.DelayFor(1).TotalMilliseconds, 100, 150);
        Assert.InRange(policy.DelayFor(2).TotalMilliseconds, 200, 300);
        Assert.InRange(policy.DelayFor(3).TotalMilliseconds, 400, 600);
    }
}

/// <summary>
/// Interblocage réel sur MariaDB : deux transactions verrouillent les deux mêmes
/// lignes dans l'ordre inverse. InnoDB en annule une (erreur 1213).
/// </summary>
public class MariaDbDeadlockTests : ProviderTests
{
    protected override Task<TestDatabase?> CreateDatabaseAsync()
        => TestDatabase.TryCreateMariaDbAsync();

    /// <summary>
    /// Lance les deux transactions croisées. Chacune verrouille sa première ligne,
    /// attend que l'autre ait verrouillé la sienne, puis demande la seconde.
    /// </summary>
    private (Task Premiere, Task Seconde, Func<int> Essais) LancerTransactionsCroisees(
        int a,
        int b,
        RetryPolicy policy)
    {
        var essais = 0;
        var premiereVerrouillee = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondeVerrouillee = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Transaction(int premier, int second, string titre, TaskCompletionSource moi, TaskCompletionSource autre)
            => Db.UnitOfWork.ExecuteInTransactionAsync(
                async uow =>
                {
                    Interlocked.Increment(ref essais);

                    await Db.Documents.RenameAsync(premier, titre, uow);
                    moi.TrySetResult();
                    await autre.Task;
                    await Db.Documents.RenameAsync(second, titre, uow);
                },
                policy);

        return (
            Transaction(a, b, "première", premiereVerrouillee, secondeVerrouillee),
            Transaction(b, a, "seconde", secondeVerrouillee, premiereVerrouillee),
            () => essais);
    }

    [Fact]
    public async Task Un_interblocage_est_repris_et_les_deux_transactions_aboutissent()
    {
        var a = await Db.Documents.InsertAsync(new Document { Titre = "a" });
        var b = await Db.Documents.InsertAsync(new Document { Titre = "b" });

        var (premiere, seconde, essais) = LancerTransactionsCroisees(a, b, RetryPolicy.Default);

        await Task.WhenAll(premiere, seconde).WaitAsync(TimeSpan.FromSeconds(30));

        // Deux essais initiaux, plus au moins une reprise de la transaction annulée.
        Assert.True(essais() >= 3, $"essais : {essais()}");

        // Chaque transaction a écrit ses deux lignes d'un bloc : la dernière validée
        // a posé son titre sur les deux.
        var titreA = (await Db.Documents.GetByIdAsync(a))!.Titre;
        var titreB = (await Db.Documents.GetByIdAsync(b))!.Titre;

        Assert.Equal(titreA, titreB);
        Assert.Contains(titreA, new[] { "première", "seconde" });
    }

    [Fact]
    public async Task Sans_reprise_l_interblocage_remonte_comme_Deadlock()
    {
        var a = await Db.Documents.InsertAsync(new Document { Titre = "a" });
        var b = await Db.Documents.InsertAsync(new Document { Titre = "b" });

        var (premiere, seconde, essais) = LancerTransactionsCroisees(a, b, RetryPolicy.None);

        var erreur = await Assert.ThrowsAsync<DataAccessException>(
            () => Task.WhenAll(premiere, seconde).WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Equal(DataAccessErrorKind.Deadlock, erreur.Kind);
        Assert.True(erreur.IsTransient);
        Assert.Equal(2, essais());

        // La transaction survivante a écrit ses deux lignes.
        Assert.Equal(
            (await Db.Documents.GetByIdAsync(a))!.Titre,
            (await Db.Documents.GetByIdAsync(b))!.Titre);
    }
}
