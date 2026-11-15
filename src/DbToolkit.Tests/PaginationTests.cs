using DbToolkit.Models;
using System.Text.Json;

namespace DbToolkit.Tests;

public abstract class PaginationTests : ProviderTests
{
    // Nom, Quantite, Note
    private static readonly (string Nom, int Quantite, string? Note)[] Seed =
    [
        ("Alpha", 1, null),
        ("Bravo", 2, ""),
        ("Charlie", 3, "x"),
        ("Delta", 4, "y"),
        ("Echo", 5, "z"),
        ("100%", 6, "n"),
        ("a_b", 7, "n")
    ];

    protected override async Task SeedAsync()
    {
        foreach (var (nom, quantite, note) in Seed)
        {
            await Db.Items.InsertAsync(
                new Item { Nom = nom, Quantite = quantite, Note = note });
        }
    }

    private static readonly SortCriteria[] ParQuantite = [new("Quantite")];

    [Fact]
    public async Task Premiere_page_renvoie_les_lignes_et_le_total()
    {
        var page = await Db.Items.GetPagedAsync(
            new PageRequest { Take = 3, Sorts = ParQuantite });

        Assert.Equal(["Alpha", "Bravo", "Charlie"], page.Items.Select(i => i.Nom));
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(1, page.PageNumber);
        Assert.True(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }

    [Fact]
    public async Task ForPage_renvoie_la_derniere_page_partielle()
    {
        var page = await Db.Items.GetPagedAsync(
            PageRequest.ForPage(3, 3) with { Sorts = ParQuantite });

        Assert.Equal(["a_b"], page.Items.Select(i => i.Nom));
        Assert.Equal(3, page.PageNumber);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Page_au_dela_de_la_fin_est_vide_mais_garde_le_total()
    {
        var page = await Db.Items.GetPagedAsync(new PageRequest { Skip = 100, Take = 10 });

        Assert.Empty(page.Items);
        Assert.Equal(7, page.TotalCount);
    }

    [Fact]
    public async Task Sans_tri_demande_le_tri_par_defaut_s_applique()
    {
        // DefaultOrderBy de ItemRepository : "Quantite DESC"
        var page = await Db.Items.GetPagedAsync(new PageRequest());

        Assert.Equal("a_b", page.Items[0].Nom);
    }

    [Fact]
    public async Task GetAll_applique_le_tri_par_defaut()
    {
        var items = await Db.Items.GetAllAsync();

        Assert.Equal(
            ["a_b", "100%", "Echo", "Delta", "Charlie", "Bravo", "Alpha"],
            items.Select(i => i.Nom));
    }

    [Fact]
    public async Task GetAll_sans_tri_par_defaut_renvoie_toutes_les_lignes()
    {
        Assert.Equal(7, (await Db.BareItems.GetAllAsync()).Count);
    }

    [Fact]
    public async Task GetAll_dans_une_transaction_applique_aussi_le_tri()
    {
        await using var uow = await Db.UnitOfWork.BeginAsync();
        await Db.Items.InsertAsync(new Item { Nom = "Zulu", Quantite = 99 }, uow);

        var items = await Db.Items.GetAllAsync(uow);

        Assert.Equal("Zulu", items[0].Nom);
        Assert.Equal(8, items.Count);
    }

    [Fact]
    public async Task Repository_sans_tri_par_defaut_ne_provoque_pas_d_erreur()
    {
        var page = await Db.BareItems.GetPagedAsync(new PageRequest());

        Assert.Equal(7, page.TotalCount);
    }

    [Fact]
    public async Task Tri_descendant()
    {
        var page = await Db.Items.GetPagedAsync(
            new PageRequest { Sorts = [new SortCriteria("Quantite", Descending: true)] });

        Assert.Equal("a_b", page.Items[0].Nom);
        Assert.Equal("Alpha", page.Items[^1].Nom);
    }

    [Fact]
    public async Task Tri_multiple_respecte_l_ordre_des_criteres()
    {
        var page = await Db.Items.GetPagedAsync(new PageRequest
        {
            Sorts = [new SortCriteria("Note", Descending: true), new SortCriteria("Quantite", Descending: true)]
        });

        // Note DESC : z, y, x, n, n, "", NULL ; à égalité, Quantite DESC.
        Assert.Equal(
            ["Echo", "Delta", "Charlie", "a_b", "100%", "Bravo", "Alpha"],
            page.Items.Select(i => i.Nom));
    }

    [Theory]
    [InlineData("%", "100%")]
    [InlineData("_", "a_b")]
    [InlineData("ALPHA", "Alpha")]
    [InlineData("  rav  ", "Bravo")]
    public async Task Recherche_traite_les_jokers_comme_du_texte(string terme, string attendu)
    {
        var page = await Db.Items.GetPagedAsync(new PageRequest { Search = terme });

        Assert.Equal([attendu], page.Items.Select(i => i.Nom));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Recherche_non_configuree_est_refusee()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Db.BareItems.GetPagedAsync(new PageRequest { Search = "a" }));
    }

    [Theory]
    [InlineData("Nom", FilterOperator.Contains, "a", 5)]
    [InlineData("Nom", FilterOperator.NotContains, "a", 2)]
    [InlineData("Nom", FilterOperator.StartsWith, "a", 2)]
    [InlineData("Nom", FilterOperator.EndsWith, "a", 2)]
    [InlineData("Nom", FilterOperator.Equals, "Echo", 1)]
    [InlineData("Nom", FilterOperator.NotEquals, "Echo", 6)]
    [InlineData("Nom", FilterOperator.Contains, "%", 1)]
    [InlineData("Quantite", FilterOperator.Equals, 3, 1)]
    [InlineData("Quantite", FilterOperator.Equals, "3", 1)]
    [InlineData("Quantite", FilterOperator.GreaterThan, 5, 2)]
    [InlineData("Quantite", FilterOperator.GreaterThanOrEqual, 5, 3)]
    [InlineData("Quantite", FilterOperator.LessThan, 2, 1)]
    [InlineData("Quantite", FilterOperator.LessThanOrEqual, 2, 2)]
    [InlineData("Note", FilterOperator.IsEmpty, null, 2)]
    [InlineData("Note", FilterOperator.IsNotEmpty, null, 5)]
    public async Task Filtre_par_operateur(
        string colonne,
        FilterOperator operateur,
        object? valeur,
        int attendu)
    {
        var page = await Db.Items.GetPagedAsync(new PageRequest
        {
            Filters = [new FilterCriteria(colonne, operateur, valeur)]
        });

        Assert.Equal(attendu, page.TotalCount);
        Assert.Equal(attendu, page.Items.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Filtre_sans_valeur_est_ignore(string? valeur)
    {
        var page = await Db.Items.GetPagedAsync(new PageRequest
        {
            Filters = [new FilterCriteria("Nom", FilterOperator.Contains, valeur)]
        });

        Assert.Equal(7, page.TotalCount);
    }

    [Theory]
    [InlineData("5", FilterOperator.GreaterThan, "Quantite", 2)]
    [InlineData("\"Echo\"", FilterOperator.Equals, "Nom", 1)]
    public async Task Valeur_de_filtre_recue_en_json(
        string json,
        FilterOperator operateur,
        string colonne,
        int attendu)
    {
        using var document = JsonDocument.Parse(json);

        var page = await Db.Items.GetPagedAsync(new PageRequest
        {
            Filters = [new FilterCriteria(colonne, operateur, document.RootElement)]
        });

        Assert.Equal(attendu, page.TotalCount);
    }

    [Fact]
    public async Task PageRequest_complet_se_desserialise_depuis_du_json()
    {
        const string json = """
            {
              "skip": 0,
              "take": 5,
              "search": "a",
              "sorts": [ { "column": "Quantite", "descending": true } ],
              "filters": [ { "column": "Quantite", "operator": 6, "value": 2 } ]
            }
            """;

        var request = JsonSerializer.Deserialize<PageRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var page = await Db.Items.GetPagedAsync(request);

        // Contient « a » et Quantite > 2 : Charlie (3), Delta (4), a_b (7).
        Assert.Equal(["a_b", "Delta", "Charlie"], page.Items.Select(i => i.Nom));
    }

    [Fact]
    public async Task Recherche_filtres_et_tri_se_combinent()
    {
        var page = await Db.Items.GetPagedAsync(new PageRequest
        {
            Search = "a",
            Filters =
            [
                new FilterCriteria("Quantite", FilterOperator.GreaterThan, 1),
                new FilterCriteria("Note", FilterOperator.IsNotEmpty)
            ],
            Sorts = [new SortCriteria("Quantite", Descending: true)],
            Take = 2
        });

        // Contient « a », Quantite > 1, Note renseignée : Charlie, Delta, a_b.
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["a_b", "Delta"], page.Items.Select(i => i.Nom));
    }

    [Fact]
    public async Task Colonne_de_tri_hors_liste_blanche_est_refusee()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Db.Items.GetPagedAsync(new PageRequest
            {
                Sorts = [new SortCriteria("Nom; DROP TABLE Item")]
            }));

        Assert.Equal(7, (await Db.Items.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Colonne_de_filtre_hors_liste_blanche_est_refusee()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Db.Items.GetPagedAsync(new PageRequest
            {
                Filters = [new FilterCriteria("Inconnue", FilterOperator.Equals, 1)]
            }));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 10)]
    public async Task Skip_ou_take_invalide_est_refuse(int skip, int take)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Db.Items.GetPagedAsync(new PageRequest { Skip = skip, Take = take }));
    }

    [Fact]
    public async Task Pagination_dans_une_transaction_voit_les_lignes_non_validees()
    {
        await using (var uow = await Db.UnitOfWork.BeginAsync())
        {
            await Db.Items.InsertAsync(new Item { Nom = "temp" }, uow);

            var dedans = await Db.Items.GetPagedAsync(new PageRequest(), uow);
            Assert.Equal(8, dedans.TotalCount);
        }

        var apres = await Db.Items.GetPagedAsync(new PageRequest());
        Assert.Equal(7, apres.TotalCount);
    }
}

public class SqlitePaginationTests : PaginationTests
{
    protected override async Task<TestDatabase?> CreateDatabaseAsync()
        => await TestDatabase.CreateSqliteAsync();
}

public class MariaDbPaginationTests : PaginationTests
{
    protected override Task<TestDatabase?> CreateDatabaseAsync()
        => TestDatabase.TryCreateMariaDbAsync();
}
