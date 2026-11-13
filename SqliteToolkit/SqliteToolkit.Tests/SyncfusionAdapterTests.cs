using SqliteToolkit.Adapters.Syncfusion;
using SqliteToolkit.Models;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;

namespace SqliteToolkit.Tests;

/// <summary>
/// L'adaptateur est comparé au moteur en mémoire de Syncfusion
/// (<see cref="DataOperations"/>) : la même demande doit donner
/// les mêmes lignes en SQL et dans les composants.
/// </summary>
public class SyncfusionAdapterTests : IAsyncLifetime
{
    private TestDatabase _db = null!;

    private static readonly Item[] Seed =
    [
        new() { Nom = "Alpha", Quantite = 1, Note = null },
        new() { Nom = "Bravo", Quantite = 2, Note = "" },
        new() { Nom = "Charlie", Quantite = 3, Note = "x" },
        new() { Nom = "Delta", Quantite = 4, Note = "y" },
        new() { Nom = "Echo", Quantite = 5, Note = "z" },
        new() { Nom = "Foxtrot", Quantite = 6, Note = "n" },
        new() { Nom = "Golf", Quantite = 7, Note = "n" }
    ];

    public async Task InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync();

        foreach (var item in Seed)
        {
            await _db.Items.InsertAsync(
                new Item { Nom = item.Nom, Quantite = item.Quantite, Note = item.Note });
        }
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Theory]
    [InlineData("Nom", "contains", "a")]
    [InlineData("Nom", "doesnotcontain", "a")]
    [InlineData("Nom", "startswith", "c")]
    [InlineData("Nom", "endswith", "a")]
    [InlineData("Nom", "equal", "Echo")]
    [InlineData("Nom", "notequal", "Echo")]
    [InlineData("Quantite", "equal", 3)]
    [InlineData("Quantite", "notequal", 3)]
    [InlineData("Quantite", "greaterthan", 5)]
    [InlineData("Quantite", "greaterthanorequal", 5)]
    [InlineData("Quantite", "lessthan", 2)]
    [InlineData("Quantite", "lessthanorequal", 2)]
    public async Task Filtre_donne_les_memes_lignes_que_le_moteur_syncfusion(
        string champ,
        string operateur,
        object valeur)
    {
        var where = new List<WhereFilter>
        {
            new() { Field = champ, Operator = operateur, value = valeur, IgnoreCase = true }
        };

        var attendu = DataOperations
            .PerformFiltering(Seed.AsEnumerable(), where, "and")
            .Select(i => i.Nom)
            .Order()
            .ToList();

        var page = await _db.Items.GetPagedAsync(
            new DataManagerRequest { Where = where }.ToPageRequest());

        Assert.NotEmpty(attendu);
        Assert.Equal(attendu, page.Items.Select(i => i.Nom).Order());
    }

    [Fact]
    public async Task Tri_multiple_donne_le_meme_ordre_que_le_moteur_syncfusion()
    {
        var sorted = new List<Sort>
        {
            new() { Name = "Quantite", Direction = "descending" },
            new() { Name = "Note", Direction = "ascending" }
        };

        var attendu = DataOperations
            .PerformSorting(Seed.AsEnumerable(), sorted)
            .Select(i => i.Nom)
            .ToList();

        var page = await _db.Items.GetPagedAsync(
            new DataManagerRequest { Sorted = sorted }.ToPageRequest());

        Assert.Equal(attendu, page.Items.Select(i => i.Nom));
    }

    [Fact]
    public async Task Recherche_donne_les_memes_lignes_que_le_moteur_syncfusion()
    {
        var search = new List<SearchFilter>
        {
            new() { Fields = ["Nom"], Key = "ra", Operator = "contains", IgnoreCase = true }
        };

        var attendu = DataOperations
            .PerformSearching(Seed.AsEnumerable(), search)
            .Select(i => i.Nom)
            .Order()
            .ToList();

        var page = await _db.Items.GetPagedAsync(
            new DataManagerRequest { Search = search }.ToPageRequest());

        Assert.Equal(["Bravo"], attendu);
        Assert.Equal(attendu, page.Items.Select(i => i.Nom).Order());
    }

    [Fact]
    public void Skip_et_take_sont_repris()
    {
        var request = new DataManagerRequest { Skip = 40, Take = 20 }.ToPageRequest();

        Assert.Equal(40, request.Skip);
        Assert.Equal(20, request.Take);
        Assert.Empty(request.Sorts);
        Assert.Empty(request.Filters);
        Assert.Null(request.Search);
    }

    [Fact]
    public async Task Take_a_zero_renvoie_toutes_les_lignes()
    {
        var page = await _db.Items.GetPagedAsync(new DataManagerRequest().ToPageRequest());

        Assert.Equal(7, page.Items.Count);
    }

    [Fact]
    public void Predicats_imbriques_par_ET_sont_aplatis()
    {
        var where = new List<WhereFilter>
        {
            new()
            {
                IsComplex = true,
                Condition = "and",
                predicates =
                [
                    new() { Field = "Nom", Operator = "contains", value = "a" },
                    new() { Field = "Quantite", Operator = "greaterthan", value = 2 }
                ]
            }
        };

        var request = new DataManagerRequest { Where = where }.ToPageRequest();

        Assert.Equal(
            [
                new FilterCriteria("Nom", FilterOperator.Contains, "a"),
                new FilterCriteria("Quantite", FilterOperator.GreaterThan, 2)
            ],
            request.Filters);
    }

    [Fact]
    public void Predicats_combines_par_OU_sont_refuses()
    {
        var where = new List<WhereFilter>
        {
            new()
            {
                IsComplex = true,
                Condition = "or",
                predicates =
                [
                    new() { Field = "Nom", Operator = "equal", value = "Alpha" },
                    new() { Field = "Nom", Operator = "equal", value = "Bravo" }
                ]
            }
        };

        Assert.Throws<NotSupportedException>(
            () => new DataManagerRequest { Where = where }.ToPageRequest());
    }

    [Fact]
    public void Operateur_inconnu_est_refuse()
    {
        var where = new List<WhereFilter>
        {
            new() { Field = "Nom", Operator = "wildcard", value = "a*" }
        };

        Assert.Throws<NotSupportedException>(
            () => new DataManagerRequest { Where = where }.ToPageRequest());
    }

    [Theory]
    [InlineData("equal", FilterOperator.IsEmpty)]
    [InlineData("notequal", FilterOperator.IsNotEmpty)]
    [InlineData("isnull", FilterOperator.IsEmpty)]
    [InlineData("isnotempty", FilterOperator.IsNotEmpty)]
    public void Filtre_sur_valeur_absente_devient_vide_ou_non_vide(
        string operateur,
        FilterOperator attendu)
    {
        var where = new List<WhereFilter>
        {
            new() { Field = "Note", Operator = operateur, value = null }
        };

        var request = new DataManagerRequest { Where = where }.ToPageRequest();

        Assert.Equal(attendu, Assert.Single(request.Filters).Operator);
    }

    [Fact]
    public void Resultat_avec_total_si_le_composant_le_demande()
    {
        var page = new PagedResult<Item>
        {
            Items = [new Item { Nom = "a" }],
            PageNumber = 1,
            PageSize = 10,
            TotalCount = 42
        };

        var avecTotal = page.ToDataResult(new DataManagerRequest { RequiresCounts = true });
        var sansTotal = page.ToDataResult(new DataManagerRequest { RequiresCounts = false });

        var data = Assert.IsType<DataResult>(avecTotal);
        Assert.Equal(42, data.Count);
        Assert.Same(page.Items, sansTotal);
    }
}
