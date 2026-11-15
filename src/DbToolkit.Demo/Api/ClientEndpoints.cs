using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using DbToolkit.Adapters.AspNetCore;
using DbToolkit.Demo.Models;
using DbToolkit.Demo.Services;
using DbToolkit.Models;

namespace DbToolkit.Demo.Api;

/// <summary>
/// API REST des clients, sur le même service que les grilles.
/// </summary>
public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        // Les erreurs de DbToolkit (colonne interdite, doublon...) deviennent
        // des réponses ProblemDetails pour tout le groupe.
        var clients = app.MapGroup("/api/clients")
            .WithDbToolkitProblems()
            // L'application remplace les 404 par sa page HTML « introuvable »
            // (UseStatusCodePagesWithReExecute) : l'API doit garder les siens.
            .WithMetadata(new SkipStatusCodePagesAttribute());

        // GET /api/clients?page=1&pageSize=20&search=dupont&sort=Nom,-Id&filter=Email:endswith:.fr
        clients.MapGet("/", async (
            [AsParameters] PageQuery query,
            PageRequestLimits limits,
            ClientService service,
            CancellationToken cancellationToken) =>
            TypedResults.Ok(await service.GetPagedAsync(
                query.ToPageRequest(limits),
                cancellationToken)));

        // POST /api/clients/search : la même demande, en JSON (filtres typés).
        clients.MapPost("/search", async (
            PageRequest request,
            PageRequestLimits limits,
            ClientService service,
            CancellationToken cancellationToken) =>
            TypedResults.Ok(await service.GetPagedAsync(
                limits.Validate(request),
                cancellationToken)));

        clients.MapGet("/{id:int}", async Task<Results<Ok<Client>, NotFound>> (
            int id,
            ClientService service,
            CancellationToken cancellationToken) =>
            await service.GetByIdAsync(id, cancellationToken) is { } client
                ? TypedResults.Ok(client)
                : TypedResults.NotFound());

        clients.MapPost("/", async Task<Results<Created<Client>, ValidationProblem>> (
            Client client,
            ClientService service,
            CancellationToken cancellationToken) =>
        {
            if (Validate(client) is { } errors)
                return TypedResults.ValidationProblem(errors);

            client.Id = await service.InsertAsync(client, cancellationToken);

            return TypedResults.Created($"/api/clients/{client.Id}", client);
        });

        clients.MapPut("/{id:int}", async Task<Results<NoContent, NotFound, ValidationProblem>> (
            int id,
            Client client,
            ClientService service,
            CancellationToken cancellationToken) =>
        {
            if (Validate(client) is { } errors)
                return TypedResults.ValidationProblem(errors);

            client.Id = id;

            return await service.UpdateAsync(client, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
        });

        clients.MapDelete("/{id:int}", async Task<Results<NoContent, NotFound>> (
            int id,
            ClientService service,
            CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());

        return app;
    }

    private static Dictionary<string, string[]>? Validate(Client client)
    {
        return string.IsNullOrWhiteSpace(client.Nom)
            ? new Dictionary<string, string[]> { ["nom"] = ["Le nom est obligatoire."] }
            : null;
    }
}
