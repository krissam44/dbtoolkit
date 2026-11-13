using MudBlazor;
using SqliteToolkit.Adapters.MudBlazor;
using SqliteToolkit.Models;

namespace SqliteToolkit.Tests;

public class MudDataGridExtensionsTests
{
    [Fact]
    public void Page_et_taille_deviennent_skip_et_take()
    {
        var state = new GridState<Item> { Page = 2, PageSize = 25 };

        var request = state.ToPageRequest();

        Assert.Equal(50, request.Skip);
        Assert.Equal(25, request.Take);
        Assert.Empty(request.Sorts);
        Assert.Empty(request.Filters);
        Assert.Null(request.Search);
    }

    [Fact]
    public void Tris_sont_repris_dans_l_ordre_de_leur_index()
    {
        var state = new GridState<Item>
        {
            PageSize = 10,
            SortDefinitions =
            [
                new SortDefinition<Item>("Quantite", true, 1, x => x.Quantite),
                new SortDefinition<Item>("Nom", false, 0, x => x.Nom)
            ]
        };

        var request = state.ToPageRequest();

        Assert.Equal(
            [new SortCriteria("Nom"), new SortCriteria("Quantite", Descending: true)],
            request.Sorts);
    }

    [Fact]
    public void Recherche_est_transmise()
    {
        var state = new GridState<Item> { PageSize = 10 };

        Assert.Equal("dupont", state.ToPageRequest("dupont").Search);
    }

    [Fact]
    public void Filtre_sans_colonne_est_ignore()
    {
        var state = new GridState<Item>
        {
            PageSize = 10,
            FilterDefinitions =
            [
                new FilterDefinition<Item>
                {
                    Operator = MudBlazor.FilterOperator.String.Contains,
                    Value = "a"
                }
            ]
        };

        Assert.Empty(state.ToPageRequest().Filters);
    }

    [Fact]
    public void Taille_de_page_nulle_ne_provoque_pas_d_erreur()
    {
        var state = new GridState<Item> { Page = 0, PageSize = 0 };

        var request = state.ToPageRequest();

        Assert.Equal(0, request.Skip);
        Assert.Equal(1, request.Take);
    }

    [Fact]
    public void Resultat_pagine_devient_GridData()
    {
        var result = new PagedResult<Item>
        {
            Items = [new Item { Nom = "a" }, new Item { Nom = "b" }],
            PageNumber = 1,
            PageSize = 2,
            TotalCount = 42
        };

        var data = result.ToGridData();

        Assert.Equal(42, data.TotalItems);
        Assert.Equal(["a", "b"], data.Items.Select(i => i.Nom));
    }
}
