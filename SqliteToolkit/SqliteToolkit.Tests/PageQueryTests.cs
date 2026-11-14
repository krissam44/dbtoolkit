using SqliteToolkit.Adapters.AspNetCore;
using SqliteToolkit.Models;

namespace SqliteToolkit.Tests;

public class PageQueryTests
{
    [Fact]
    public void Sans_parametre_renvoie_la_premiere_page_par_defaut()
    {
        var request = new PageQuery().ToPageRequest();

        Assert.Equal(0, request.Skip);
        Assert.Equal(PageQuery.DefaultPageSize, request.Take);
        Assert.Empty(request.Sorts);
        Assert.Empty(request.Filters);
        Assert.Null(request.Search);
    }

    [Fact]
    public void Page_et_pageSize_deviennent_skip_et_take()
    {
        var request = new PageQuery(Page: 3, PageSize: 25).ToPageRequest();

        Assert.Equal(50, request.Skip);
        Assert.Equal(25, request.Take);
    }

    [Fact]
    public void Skip_et_take_sont_repris_tels_quels()
    {
        var request = new PageQuery(Skip: 40, Take: 10).ToPageRequest();

        Assert.Equal(40, request.Skip);
        Assert.Equal(10, request.Take);
    }

    [Theory]
    [InlineData(1, null, 0, null)]
    [InlineData(null, 10, null, 5)]
    public void Melanger_page_et_skip_est_refuse(int? page, int? pageSize, int? skip, int? take)
    {
        Assert.Throws<ArgumentException>(
            () => new PageQuery(page, pageSize, skip, take).ToPageRequest());
    }

    [Theory]
    [InlineData(0, 10, null, null)]
    [InlineData(1, 0, null, null)]
    [InlineData(null, null, -1, 10)]
    [InlineData(null, null, 0, 0)]
    [InlineData(int.MaxValue, int.MaxValue, null, null)]
    public void Pagination_invalide_est_refusee(int? page, int? pageSize, int? skip, int? take)
    {
        Assert.Throws<ArgumentException>(
            () => new PageQuery(page, pageSize, skip, take).ToPageRequest());
    }

    [Fact]
    public void Tris_acceptent_virgules_prefixe_et_suffixe()
    {
        var request = new PageQuery(Sort: ["Nom,-Id", "Email:desc", "+Prenom:asc"]).ToPageRequest();

        Assert.Equal(
            [
                new SortCriteria("Nom"),
                new SortCriteria("Id", Descending: true),
                new SortCriteria("Email", Descending: true),
                new SortCriteria("Prenom")
            ],
            request.Sorts);
    }

    [Theory]
    [InlineData("Nom:vers-le-bas")]
    [InlineData("-")]
    [InlineData(":desc")]
    public void Tri_invalide_est_refuse(string sort)
    {
        Assert.Throws<ArgumentException>(
            () => new PageQuery(Sort: [sort]).ToPageRequest());
    }

    [Fact]
    public void Filtres_sont_decoupes_en_colonne_operateur_valeur()
    {
        var request = new PageQuery(
            Filter: ["Nom:contains:du", "Quantite:GTE:5", "Note:empty", "Heure:eq:12:30:00"])
            .ToPageRequest();

        Assert.Equal(
            [
                new FilterCriteria("Nom", FilterOperator.Contains, "du"),
                new FilterCriteria("Quantite", FilterOperator.GreaterThanOrEqual, "5"),
                new FilterCriteria("Note", FilterOperator.IsEmpty),
                new FilterCriteria("Heure", FilterOperator.Equals, "12:30:00")
            ],
            request.Filters);
    }

    [Theory]
    [InlineData("Nom")]
    [InlineData(":contains:a")]
    [InlineData("Nom:ressemble:a")]
    public void Filtre_invalide_est_refuse(string filter)
    {
        Assert.Throws<ArgumentException>(
            () => new PageQuery(Filter: [filter]).ToPageRequest());
    }

    [Fact]
    public void Recherche_vide_est_ignoree_et_les_espaces_sont_retires()
    {
        Assert.Null(new PageQuery(Search: "   ").ToPageRequest().Search);
        Assert.Equal("dupont", new PageQuery(Search: "  dupont ").ToPageRequest().Search);
    }

    [Fact]
    public void Bornes_par_defaut_refusent_une_page_trop_grande()
    {
        var limits = new PageRequestLimits();

        Assert.Throws<ArgumentException>(
            () => new PageQuery(Take: limits.MaxPageSize + 1).ToPageRequest(limits));

        Assert.Equal(
            limits.MaxPageSize,
            new PageQuery(Take: limits.MaxPageSize).ToPageRequest(limits).Take);
    }

    [Fact]
    public void Bornes_refusent_trop_de_filtres_de_tris_ou_une_recherche_trop_longue()
    {
        var limits = new PageRequestLimits { MaxFilters = 1, MaxSorts = 1, MaxSearchLength = 3 };

        Assert.Throws<ArgumentException>(
            () => new PageQuery(Filter: ["A:eq:1", "B:eq:2"]).ToPageRequest(limits));

        Assert.Throws<ArgumentException>(
            () => new PageQuery(Sort: ["A,B"]).ToPageRequest(limits));

        Assert.Throws<ArgumentException>(
            () => new PageQuery(Search: "abcd").ToPageRequest(limits));
    }
}
