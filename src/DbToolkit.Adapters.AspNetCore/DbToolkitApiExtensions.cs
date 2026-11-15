using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DbToolkit.Models;

namespace DbToolkit.Adapters.AspNetCore;

public static class DbToolkitApiExtensions
{
    /// <summary>
    /// Enregistre les <see cref="PageRequestLimits"/> injectés dans les endpoints.
    /// </summary>
    /// <param name="limits">Bornes à appliquer ; <see cref="PageRequestLimits.Default"/> si absent.</param>
    public static IServiceCollection AddDbToolkitApi(
        this IServiceCollection services,
        PageRequestLimits? limits = null)
    {
        services.AddSingleton(limits ?? PageRequestLimits.Default);

        return services;
    }

    /// <summary>
    /// Convertit les exceptions de DbToolkit levées par les endpoints en réponses
    /// ProblemDetails (voir <see cref="DbToolkitProblems"/>). S'applique à un
    /// endpoint ou à tout un groupe (<c>app.MapGroup(...).WithDbToolkitProblems()</c>),
    /// sans toucher au reste du pipeline de l'application.
    /// </summary>
    public static TBuilder WithDbToolkitProblems<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter<TBuilder, DbToolkitProblemFilter>();
    }
}

internal sealed class DbToolkitProblemFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception) when (DbToolkitProblems.TryCreate(exception, out var problem))
        {
            return problem;
        }
    }
}
