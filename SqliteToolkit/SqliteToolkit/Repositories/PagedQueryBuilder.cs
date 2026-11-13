using Dapper;
using SqliteToolkit.Models;
using System.Globalization;
using System.Text.Json;

namespace SqliteToolkit.Repositories;

internal sealed record PagedQuery(
    string ItemsSql,
    string CountSql,
    DynamicParameters Parameters);

/// <summary>
/// Traduit un <see cref="PageRequest"/> en SQL paramétré.
/// Seules les colonnes présentes dans les listes blanches atteignent le SQL.
/// </summary>
internal static class PagedQueryBuilder
{
    private const string LikeEscape = " ESCAPE '\\'";

    public static PagedQuery Build(
        string baseSql,
        PageRequest request,
        IReadOnlyDictionary<string, string> sortColumns,
        IReadOnlyDictionary<string, string> filterColumns,
        IReadOnlyList<string> searchColumns,
        string defaultOrderBy)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(request.Skip, nameof(request.Skip));
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Take, 1, nameof(request.Take));

        var parameters = new DynamicParameters();
        parameters.Add("Take", request.Take);
        parameters.Add("Skip", request.Skip);

        var conditions = new List<string>();

        AddSearch(request.Search, searchColumns, conditions, parameters);
        AddFilters(request.Filters, filterColumns, conditions, parameters);

        var where = conditions.Count > 0
            ? $"WHERE {string.Join(" AND ", conditions)}"
            : string.Empty;

        var orderBy = BuildOrderBy(request.Sorts, sortColumns, defaultOrderBy);

        var source = baseSql.Trim().TrimEnd(';');

        // La requête d'origine est encapsulée : filtres et tris
        // s'appliquent sans modifier son SQL.
        var itemsSql = $"""
            SELECT *
            FROM ({source}) AS PagedQuery
            {where}
            {orderBy}
            LIMIT @Take OFFSET @Skip;
            """;

        var countSql = $"""
            SELECT COUNT(*)
            FROM ({source}) AS PagedQuery
            {where};
            """;

        return new PagedQuery(itemsSql, countSql, parameters);
    }

    private static void AddSearch(
        string? search,
        IReadOnlyList<string> searchColumns,
        List<string> conditions,
        DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(search))
            return;

        if (searchColumns.Count == 0)
        {
            throw new InvalidOperationException(
                "La recherche n'est pas configurée pour ce repository (SearchColumns est vide).");
        }

        parameters.Add("Search", $"%{EscapeLike(search.Trim())}%");

        var alternatives = searchColumns
            .Select(column => $"{column} LIKE @Search{LikeEscape}");

        conditions.Add($"({string.Join(" OR ", alternatives)})");
    }

    private static void AddFilters(
        IReadOnlyList<FilterCriteria> filters,
        IReadOnlyDictionary<string, string> filterColumns,
        List<string> conditions,
        DynamicParameters parameters)
    {
        var index = 0;

        foreach (var filter in filters)
        {
            if (!filterColumns.TryGetValue(filter.Column, out var column))
            {
                throw new ArgumentException(
                    $"Colonne de filtre non autorisée : {filter.Column}");
            }

            if (filter.Operator == FilterOperator.IsEmpty)
            {
                conditions.Add($"({column} IS NULL OR {column} = '')");
                continue;
            }

            if (filter.Operator == FilterOperator.IsNotEmpty)
            {
                conditions.Add($"({column} IS NOT NULL AND {column} <> '')");
                continue;
            }

            var value = Normalize(filter.Value);

            // Les grilles envoient des filtres sans valeur tant que
            // l'utilisateur n'a rien saisi : ils sont ignorés.
            if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
                continue;

            var name = $"Filter{index++}";
            var parameter = $"@{name}";

            switch (filter.Operator)
            {
                case FilterOperator.Contains:
                    conditions.Add($"{column} LIKE {parameter}{LikeEscape}");
                    parameters.Add(name, $"%{EscapeLike(ToText(value))}%");
                    break;

                case FilterOperator.NotContains:
                    conditions.Add($"({column} IS NULL OR {column} NOT LIKE {parameter}{LikeEscape})");
                    parameters.Add(name, $"%{EscapeLike(ToText(value))}%");
                    break;

                case FilterOperator.StartsWith:
                    conditions.Add($"{column} LIKE {parameter}{LikeEscape}");
                    parameters.Add(name, $"{EscapeLike(ToText(value))}%");
                    break;

                case FilterOperator.EndsWith:
                    conditions.Add($"{column} LIKE {parameter}{LikeEscape}");
                    parameters.Add(name, $"%{EscapeLike(ToText(value))}");
                    break;

                case FilterOperator.Equals:
                    conditions.Add($"{column} = {parameter}");
                    parameters.Add(name, value);
                    break;

                case FilterOperator.NotEquals:
                    conditions.Add($"({column} IS NULL OR {column} <> {parameter})");
                    parameters.Add(name, value);
                    break;

                case FilterOperator.GreaterThan:
                    conditions.Add($"{column} > {parameter}");
                    parameters.Add(name, value);
                    break;

                case FilterOperator.GreaterThanOrEqual:
                    conditions.Add($"{column} >= {parameter}");
                    parameters.Add(name, value);
                    break;

                case FilterOperator.LessThan:
                    conditions.Add($"{column} < {parameter}");
                    parameters.Add(name, value);
                    break;

                case FilterOperator.LessThanOrEqual:
                    conditions.Add($"{column} <= {parameter}");
                    parameters.Add(name, value);
                    break;

                default:
                    throw new ArgumentException(
                        $"Opérateur de filtre non pris en charge : {filter.Operator}");
            }
        }
    }

    private static string BuildOrderBy(
        IReadOnlyList<SortCriteria> sorts,
        IReadOnlyDictionary<string, string> sortColumns,
        string defaultOrderBy)
    {
        if (sorts.Count == 0)
        {
            return string.IsNullOrWhiteSpace(defaultOrderBy)
                ? string.Empty
                : $"ORDER BY {defaultOrderBy}";
        }

        var parts = new List<string>(sorts.Count);

        foreach (var sort in sorts)
        {
            if (!sortColumns.TryGetValue(sort.Column, out var column))
            {
                throw new ArgumentException(
                    $"Colonne de tri non autorisée : {sort.Column}");
            }

            parts.Add($"{column} {(sort.Descending ? "DESC" : "ASC")}");
        }

        return $"ORDER BY {string.Join(", ", parts)}";
    }

    /// <summary>
    /// Une valeur reçue en JSON (minimal API) arrive sous forme de
    /// <see cref="JsonElement"/> : elle est ramenée à un type simple.
    /// </summary>
    private static object? Normalize(object? value)
    {
        if (value is not JsonElement json)
            return value;

        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number => json.TryGetInt64(out var integer)
                ? integer
                : json.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => throw new ArgumentException(
                $"Valeur de filtre non prise en charge : {json.ValueKind}")
        };
    }

    private static string ToText(object value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string EscapeLike(string value)
        => value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
}
