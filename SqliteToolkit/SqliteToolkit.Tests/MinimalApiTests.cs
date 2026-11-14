using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SqliteToolkit.Adapters.AspNetCore;
using SqliteToolkit.Models;

namespace SqliteToolkit.Tests;

/// <summary>
/// Une vraie application minimale, servie en mémoire : la query string,
/// la sérialisation JSON et la traduction des erreurs sont testées de bout en bout.
/// </summary>
public class MinimalApiTests : IAsyncLifetime
{
    private string _path = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _path = TestDatabase.NewPath();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        TestDatabase.ConfigureServices(builder.Services, _path);
        builder.Services.AddSqliteToolkitApi(new PageRequestLimits { MaxPageSize = 50 });

        _app = builder.Build();

        var items = _app.MapGroup("/items").WithSqliteToolkitProblems();

        items.MapGet("/", async (
            [AsParameters] PageQuery query,
            PageRequestLimits limits,
            ItemRepository repository,
            CancellationToken cancellationToken) =>
            TypedResults.Ok(await repository.GetPagedAsync(
                query.ToPageRequest(limits),
                cancellationToken: cancellationToken)));

        items.MapPost("/search", async (
            PageRequest request,
            PageRequestLimits limits,
            ItemRepository repository) =>
            TypedResults.Ok(await repository.GetPagedAsync(limits.Validate(request))));

        items.MapPost("/", async (Item item, ItemRepository repository) =>
        {
            item.Id = await repository.InsertAsync(item);
            return TypedResults.Created($"/items/{item.Id}", item);
        });

        items.MapGet("/boom", string () => throw new InvalidOperationException("hors périmètre"));

        await TestDatabase.CreateSchemaAsync(_app.Services);

        var repository = _app.Services.GetRequiredService<ItemRepository>();

        foreach (var (nom, quantite) in new[]
                 {
                     ("Alpha", 1), ("Bravo", 2), ("Charlie", 3), ("Delta", 4),
                     ("Echo", 5), ("Foxtrot", 6), ("Golf", 7)
                 })
        {
            await repository.InsertAsync(new Item { Nom = nom, Quantite = quantite });
        }

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        TestDatabase.DeleteFile(_path);
    }

    private async Task<(int Total, List<string> Noms)> GetPageAsync(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return (
            json.GetProperty("totalCount").GetInt32(),
            json.GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("nom").GetString()!)
                .ToList());
    }

    private static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Get_pagine_et_trie_depuis_la_query_string()
    {
        var (total, noms) = await GetPageAsync("/items?page=2&pageSize=3&sort=-Quantite");

        Assert.Equal(7, total);
        Assert.Equal(["Delta", "Charlie", "Bravo"], noms);
    }

    [Fact]
    public async Task Get_combine_recherche_filtres_et_tri()
    {
        var (total, noms) = await GetPageAsync(
            "/items?search=a&filter=Quantite:gt:1&filter=Nom:notequals:Delta&sort=Nom:desc");

        // Contient « a », Quantite > 1, sauf Delta : Bravo, Charlie.
        Assert.Equal(2, total);
        Assert.Equal(["Charlie", "Bravo"], noms);
    }

    [Fact]
    public async Task Get_avec_skip_et_take()
    {
        var (total, noms) = await GetPageAsync("/items?skip=5&take=10&sort=Quantite");

        Assert.Equal(7, total);
        Assert.Equal(["Foxtrot", "Golf"], noms);
    }

    [Fact]
    public async Task Post_search_accepte_un_PageRequest_en_json()
    {
        var response = await _client.PostAsJsonAsync("/items/search", new
        {
            take = 2,
            sorts = new[] { new { column = "Quantite", descending = true } },
            filters = new[] { new { column = "Quantite", @operator = 9, value = 6 } } // LessThanOrEqual
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(6, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            ["Foxtrot", "Echo"],
            json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("nom").GetString()));
    }

    [Theory]
    [InlineData("/items?sort=MotDePasse")]
    [InlineData("/items?filter=MotDePasse:eq:1")]
    [InlineData("/items?filter=Nom:ressemble:a")]
    [InlineData("/items?pageSize=51")]
    [InlineData("/items?page=0")]
    [InlineData("/items?page=1&skip=10")]
    public async Task Get_invalide_renvoie_400_en_problem_details(string url)
    {
        var problem = await ReadProblemAsync(
            await _client.GetAsync(url),
            HttpStatusCode.BadRequest);

        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Post_search_au_dela_des_bornes_renvoie_400()
    {
        var response = await _client.PostAsJsonAsync("/items/search", new { take = 1000 });

        await ReadProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doublon_renvoie_409_sans_exposer_le_sql()
    {
        var response = await _client.PostAsJsonAsync("/items", new { nom = "Alpha", quantite = 1 });

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict);

        Assert.Equal("UniqueViolation", problem.GetProperty("kind").GetString());

        var body = problem.GetRawText();
        Assert.DoesNotContain("SQLite", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Item.Nom", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Regle_check_renvoie_400()
    {
        var response = await _client.PostAsJsonAsync("/items", new { nom = "Hotel", quantite = -5 });

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);

        Assert.Equal("CheckViolation", problem.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Creation_valide_renvoie_201()
    {
        var response = await _client.PostAsJsonAsync("/items", new { nom = "Hotel", quantite = 8 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var (total, _) = await GetPageAsync("/items");
        Assert.Equal(8, total);
    }

    [Fact]
    public async Task Exception_hors_perimetre_n_est_pas_avalee()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.GetAsync("/items/boom"));
    }
}
