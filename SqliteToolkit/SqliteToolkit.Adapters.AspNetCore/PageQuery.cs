using Microsoft.AspNetCore.Mvc;
using SqliteToolkit.Models;

namespace SqliteToolkit.Adapters.AspNetCore;

/// <summary>
/// Paramètres de pagination lus dans la query string d'un endpoint minimal :
/// <code>
/// app.MapGet("/clients", ([AsParameters] PageQuery query, PageRequestLimits limits, ...) =&gt;
///     repository.GetPagedAsync(query.ToPageRequest(limits)));
/// </code>
/// Exemple : <c>?page=2&amp;pageSize=20&amp;search=dupont&amp;sort=Nom,-Id&amp;filter=Email:endswith:@exemple.fr</c>
/// <list type="bullet">
/// <item><c>page</c> / <c>pageSize</c> (page à partir de 1) ou <c>skip</c> / <c>take</c>, au choix.</item>
/// <item><c>sort</c> : colonnes séparées par des virgules ; <c>-Nom</c> ou <c>Nom:desc</c> pour l'ordre décroissant ; le paramètre peut être répété.</item>
/// <item><c>filter</c> : <c>colonne:opérateur:valeur</c>, répétable ; la valeur peut contenir des « : ».</item>
/// </list>
/// Opérateurs : contains, notcontains, equals (eq), notequals (ne), startswith, endswith,
/// gt, gte, lt, lte, empty, notempty.
/// </summary>
public sealed record PageQuery(
    [FromQuery(Name = "page")] int? Page = null,
    [FromQuery(Name = "pageSize")] int? PageSize = null,
    [FromQuery(Name = "skip")] int? Skip = null,
    [FromQuery(Name = "take")] int? Take = null,
    [FromQuery(Name = "search")] string? Search = null,
    [FromQuery(Name = "sort")] string[]? Sort = null,
    [FromQuery(Name = "filter")] string[]? Filter = null)
{
    public const int DefaultPageSize = 20;

    private static readonly Dictionary<string, FilterOperator> Operators =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["contains"] = FilterOperator.Contains,
            ["notcontains"] = FilterOperator.NotContains,
            ["equals"] = FilterOperator.Equals,
            ["eq"] = FilterOperator.Equals,
            ["notequals"] = FilterOperator.NotEquals,
            ["ne"] = FilterOperator.NotEquals,
            ["startswith"] = FilterOperator.StartsWith,
            ["endswith"] = FilterOperator.EndsWith,
            ["gt"] = FilterOperator.GreaterThan,
            ["gte"] = FilterOperator.GreaterThanOrEqual,
            ["lt"] = FilterOperator.LessThan,
            ["lte"] = FilterOperator.LessThanOrEqual,
            ["empty"] = FilterOperator.IsEmpty,
            ["notempty"] = FilterOperator.IsNotEmpty
        };

    /// <summary>
    /// Traduit la query string en <see cref="PageRequest"/>.
    /// </summary>
    /// <param name="limits">Bornes à respecter ; <c>null</c> pour aucune.</param>
    /// <exception cref="ArgumentException">
    /// Paramètre invalide ou borne dépassée ; le message peut être renvoyé tel quel au client
    /// (voir <see cref="SqliteToolkitApiExtensions.WithSqliteToolkitProblems{TBuilder}"/>).
    /// </exception>
    public PageRequest ToPageRequest(PageRequestLimits? limits = null)
    {
        var (skip, take) = ResolvePaging();

        var request = new PageRequest
        {
            Skip = skip,
            Take = take,
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
            Sorts = ParseSorts(Sort),
            Filters = ParseFilters(Filter)
        };

        return limits is null ? request : limits.Validate(request);
    }

    private (int Skip, int Take) ResolvePaging()
    {
        var usesPage = Page.HasValue || PageSize.HasValue;
        var usesSkip = Skip.HasValue || Take.HasValue;

        if (usesPage && usesSkip)
        {
            throw new ArgumentException(
                "Utilisez soit page et pageSize, soit skip et take, pas les deux.");
        }

        if (usesPage)
        {
            var page = Page ?? 1;
            var size = PageSize ?? DefaultPageSize;

            if (page < 1)
                throw new ArgumentException("page doit être supérieur ou égal à 1.");

            if (size < 1)
                throw new ArgumentException("pageSize doit être supérieur ou égal à 1.");

            var offset = (long)(page - 1) * size;

            if (offset > int.MaxValue)
                throw new ArgumentException("La page demandée est hors limites.");

            return ((int)offset, size);
        }

        var skip = Skip ?? 0;
        var take = Take ?? DefaultPageSize;

        if (skip < 0)
            throw new ArgumentException("skip doit être supérieur ou égal à 0.");

        if (take < 1)
            throw new ArgumentException("take doit être supérieur ou égal à 1.");

        return (skip, take);
    }

    private static List<SortCriteria> ParseSorts(string[]? values)
    {
        var sorts = new List<SortCriteria>();

        foreach (var value in values ?? [])
        {
            var tokens = value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var token in tokens)
            {
                var name = token;
                var descending = false;

                if (name[0] == '-')
                {
                    descending = true;
                    name = name[1..].Trim();
                }
                else if (name[0] == '+')
                {
                    name = name[1..].Trim();
                }

                var colon = name.LastIndexOf(':');

                if (colon >= 0)
                {
                    var direction = name[(colon + 1)..].Trim();
                    name = name[..colon].Trim();

                    if (direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
                        descending = true;
                    else if (!direction.Equals("asc", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException(
                            $"Sens de tri inconnu : « {direction} ». Valeurs possibles : asc, desc.");
                }

                if (name.Length == 0)
                    throw new ArgumentException("Tri invalide : nom de colonne manquant.");

                sorts.Add(new SortCriteria(name, descending));
            }
        }

        return sorts;
    }

    private static List<FilterCriteria> ParseFilters(string[]? values)
    {
        var filters = new List<FilterCriteria>();

        foreach (var value in values ?? [])
        {
            var parts = value.Split(':', 3);

            if (parts.Length < 2 || parts[0].Trim().Length == 0)
            {
                throw new ArgumentException(
                    $"Filtre invalide : « {value} ». Format attendu : colonne:opérateur[:valeur].");
            }

            var operatorName = parts[1].Trim();

            if (!Operators.TryGetValue(operatorName, out var filterOperator))
            {
                throw new ArgumentException(
                    $"Opérateur de filtre inconnu : « {operatorName} ». " +
                    $"Valeurs possibles : {string.Join(", ", Operators.Keys)}.");
            }

            filters.Add(new FilterCriteria(
                parts[0].Trim(),
                filterOperator,
                parts.Length == 3 ? parts[2] : null));
        }

        return filters;
    }
}
