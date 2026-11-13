using global::Syncfusion.Blazor;
using global::Syncfusion.Blazor.Data;
using SqliteToolkit.Models;

namespace SqliteToolkit.Adapters.Syncfusion;

/// <summary>
/// Passerelle entre le <c>DataManagerRequest</c> des composants Syncfusion
/// (SfGrid avec un adaptateur personnalisé) et la pagination de SqliteToolkit.
/// </summary>
public static class DataManagerRequestExtensions
{
    private static readonly Dictionary<string, FilterOperator> Operators =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["equal"] = FilterOperator.Equals,
            ["notequal"] = FilterOperator.NotEquals,
            ["contains"] = FilterOperator.Contains,
            ["doesnotcontain"] = FilterOperator.NotContains,
            ["startswith"] = FilterOperator.StartsWith,
            ["endswith"] = FilterOperator.EndsWith,
            ["greaterthan"] = FilterOperator.GreaterThan,
            ["greaterthanorequal"] = FilterOperator.GreaterThanOrEqual,
            ["lessthan"] = FilterOperator.LessThan,
            ["lessthanorequal"] = FilterOperator.LessThanOrEqual,
            ["isnull"] = FilterOperator.IsEmpty,
            ["isempty"] = FilterOperator.IsEmpty,
            ["isnotnull"] = FilterOperator.IsNotEmpty,
            ["isnotempty"] = FilterOperator.IsNotEmpty
        };

    /// <summary>
    /// Traduit la demande du composant (pagination, tris, filtres, recherche)
    /// en <see cref="PageRequest"/>. Les colonnes sont désignées par leur champ.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Filtres combinés par OU, ou opérateur sans équivalent.
    /// </exception>
    public static PageRequest ToPageRequest(this DataManagerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filters = new List<FilterCriteria>();

        foreach (var where in request.Where ?? [])
            AddFilter(where, filters);

        // Même priorité que DataOperations.PerformSorting :
        // le premier tri de la liste est le tri principal.
        var sorts = (request.Sorted ?? [])
            .Select(sort => new SortCriteria(
                sort.Name,
                sort.Direction?.StartsWith("desc", StringComparison.OrdinalIgnoreCase) == true))
            .ToList();

        var search = (request.Search ?? [])
            .Select(filter => filter.Key)
            .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key));

        return new PageRequest
        {
            Skip = Math.Max(request.Skip, 0),
            // Take vaut 0 quand la pagination du composant est désactivée.
            Take = request.Take > 0 ? request.Take : int.MaxValue,
            Sorts = sorts,
            Filters = filters,
            Search = search
        };
    }

    /// <summary>
    /// Convertit une page en résultat attendu par un adaptateur Syncfusion :
    /// un <see cref="DataResult"/> si le composant demande le total, sinon les lignes seules.
    /// </summary>
    public static object ToDataResult<T>(
        this PagedResult<T> result,
        DataManagerRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(request);

        return request.RequiresCounts
            ? new DataResult { Result = result.Items, Count = result.TotalCount }
            : result.Items;
    }

    private static void AddFilter(WhereFilter where, List<FilterCriteria> filters)
    {
        if (where.IsComplex)
        {
            var predicates = where.predicates ?? [];

            if (predicates.Count > 1 &&
                string.Equals(where.Condition, "or", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "Les filtres combinés par OU ne sont pas pris en charge.");
            }

            foreach (var predicate in predicates)
                AddFilter(predicate, filters);

            return;
        }

        if (string.IsNullOrEmpty(where.Field))
            return;

        if (!Operators.TryGetValue(where.Operator ?? string.Empty, out var filterOperator))
        {
            throw new NotSupportedException(
                $"Opérateur de filtre Syncfusion non pris en charge : {where.Operator}");
        }

        // « égal à rien » et « différent de rien » : la grille filtre sur les valeurs vides.
        if (where.value is null)
        {
            filterOperator = filterOperator switch
            {
                FilterOperator.Equals => FilterOperator.IsEmpty,
                FilterOperator.NotEquals => FilterOperator.IsNotEmpty,
                _ => filterOperator
            };
        }

        filters.Add(new FilterCriteria(where.Field, filterOperator, where.value));
    }
}
