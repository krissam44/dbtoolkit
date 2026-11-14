using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DbToolkit.Models;

namespace DbToolkit.Adapters.AspNetCore;

public static class SqliteToolkitApiExtensions
{
    /// <summary>
    /// Enregistre les <see cref="PageRequestLimits"/> injectés dans les endpoints.
    /// </summary>
    /// <param name="limits">Bornes à appliquer ; <see cref="PageRequestLimits.Default"/> si absent.</param>
    public static IServiceCollection AddSqliteToolkitApi(
        this IServiceCollection services,
        PageRequestLimits? limits = null)
    {
        services.AddSingleton(limits ?? PageRequestLimits.Default);

        return services;
    }

    /// <summary>
    /// Convertit les exceptions de SqliteToolkit levées par les endpoints en réponses
    /// ProblemDetails (voir <see cref="SqliteToolkitProblems"/>). S'applique à un
    /// endpoint ou à tout un groupe (<c>app.MapGroup(...).WithSqliteToolkitProblems()</c>),
    /// sans toucher au reste du pipeline de l'application.
    /// </summary>
    public static TBuilder WithSqliteToolkitProblems<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter<TBuilder, SqliteToolkitProblemFilter>();
    }
}

internal sealed class SqliteToolkitProblemFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception) when (SqliteToolkitProblems.TryCreate(exception, out var problem))
        {
            return problem;
        }
    }
}
