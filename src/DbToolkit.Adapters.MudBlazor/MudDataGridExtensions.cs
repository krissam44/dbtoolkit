using DbToolkit.Models;
using MudOperator = global::MudBlazor.FilterOperator;

namespace DbToolkit.Adapters.MudBlazor;

/// <summary>
/// Passerelle entre le mode serveur de <c>MudDataGrid</c> et la pagination de DbToolkit.
/// </summary>
public static class MudDataGridExtensions
{
    // Plusieurs familles d'opérateurs MudBlazor partagent le même texte
    // (« is empty », par exemple) : l'indexeur absorbe ces doublons.
    private static readonly Dictionary<string, FilterOperator> Operators = CreateOperators();

    /// <summary>
    /// Traduit l'état de la grille (page, tris, filtres) en <see cref="PageRequest"/>.
    /// Les colonnes sont désignées par le nom de leur propriété.
    /// </summary>
    /// <param name="state">État reçu par le callback <c>ServerData</c>.</param>
    /// <param name="search">Terme de recherche globale, géré hors de la grille.</param>
    public static PageRequest ToPageRequest<T>(
        this global::MudBlazor.GridState<T> state,
        string? search = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var sorts = state.SortDefinitions
            .OrderBy(sort => sort.Index)
            .Select(sort => new SortCriteria(sort.SortBy, sort.Descending))
            .ToList();

        var filters = new List<FilterCriteria>();

        foreach (var filter in state.FilterDefinitions)
        {
            var column = filter.Column?.PropertyName;

            // Filtre en cours de saisie : colonne ou opérateur pas encore choisi.
            if (string.IsNullOrEmpty(column) || string.IsNullOrEmpty(filter.Operator))
                continue;

            if (!Operators.TryGetValue(filter.Operator, out var filterOperator))
            {
                throw new NotSupportedException(
                    $"Opérateur de filtre MudBlazor non pris en charge : {filter.Operator}");
            }

            filters.Add(new FilterCriteria(column, filterOperator, filter.Value));
        }

        var pageSize = Math.Max(state.PageSize, 1);

        return new PageRequest
        {
            Skip = checked(Math.Max(state.Page, 0) * pageSize),
            Take = pageSize,
            Sorts = sorts,
            Filters = filters,
            Search = search
        };
    }

    /// <summary>
    /// Convertit une page en données attendues par <c>MudDataGrid</c>.
    /// </summary>
    public static global::MudBlazor.GridData<T> ToGridData<T>(
        this PagedResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new global::MudBlazor.GridData<T>
        {
            Items = result.Items,
            TotalItems = result.TotalCount
        };
    }

    private static Dictionary<string, FilterOperator> CreateOperators()
    {
        return new Dictionary<string, FilterOperator>(StringComparer.OrdinalIgnoreCase)
        {
            [MudOperator.String.Contains] = FilterOperator.Contains,
            [MudOperator.String.NotContains] = FilterOperator.NotContains,
            [MudOperator.String.Equal] = FilterOperator.Equals,
            [MudOperator.String.NotEqual] = FilterOperator.NotEquals,
            [MudOperator.String.StartsWith] = FilterOperator.StartsWith,
            [MudOperator.String.EndsWith] = FilterOperator.EndsWith,
            [MudOperator.String.Empty] = FilterOperator.IsEmpty,
            [MudOperator.String.NotEmpty] = FilterOperator.IsNotEmpty,

            [MudOperator.Number.Equal] = FilterOperator.Equals,
            [MudOperator.Number.NotEqual] = FilterOperator.NotEquals,
            [MudOperator.Number.GreaterThan] = FilterOperator.GreaterThan,
            [MudOperator.Number.GreaterThanOrEqual] = FilterOperator.GreaterThanOrEqual,
            [MudOperator.Number.LessThan] = FilterOperator.LessThan,
            [MudOperator.Number.LessThanOrEqual] = FilterOperator.LessThanOrEqual,
            [MudOperator.Number.Empty] = FilterOperator.IsEmpty,
            [MudOperator.Number.NotEmpty] = FilterOperator.IsNotEmpty,

            [MudOperator.Boolean.Is] = FilterOperator.Equals,

            [MudOperator.Enum.Is] = FilterOperator.Equals,
            [MudOperator.Enum.IsNot] = FilterOperator.NotEquals,
            [MudOperator.Enum.Empty] = FilterOperator.IsEmpty,
            [MudOperator.Enum.NotEmpty] = FilterOperator.IsNotEmpty,

            [MudOperator.DateTime.Is] = FilterOperator.Equals,
            [MudOperator.DateTime.IsNot] = FilterOperator.NotEquals,
            [MudOperator.DateTime.After] = FilterOperator.GreaterThan,
            [MudOperator.DateTime.OnOrAfter] = FilterOperator.GreaterThanOrEqual,
            [MudOperator.DateTime.Before] = FilterOperator.LessThan,
            [MudOperator.DateTime.OnOrBefore] = FilterOperator.LessThanOrEqual,
            [MudOperator.DateTime.Empty] = FilterOperator.IsEmpty,
            [MudOperator.DateTime.NotEmpty] = FilterOperator.IsNotEmpty,

            [MudOperator.DateOnly.Is] = FilterOperator.Equals,
            [MudOperator.DateOnly.IsNot] = FilterOperator.NotEquals,
            [MudOperator.DateOnly.After] = FilterOperator.GreaterThan,
            [MudOperator.DateOnly.OnOrAfter] = FilterOperator.GreaterThanOrEqual,
            [MudOperator.DateOnly.Before] = FilterOperator.LessThan,
            [MudOperator.DateOnly.OnOrBefore] = FilterOperator.LessThanOrEqual,
            [MudOperator.DateOnly.Empty] = FilterOperator.IsEmpty,
            [MudOperator.DateOnly.NotEmpty] = FilterOperator.IsNotEmpty,

            [MudOperator.Guid.Equal] = FilterOperator.Equals,
            [MudOperator.Guid.NotEqual] = FilterOperator.NotEquals
        };
    }
}
