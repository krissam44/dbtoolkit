namespace DbToolkit.Models;

/// <summary>
/// Demande de page indépendante de l'interface : pagination, tris,
/// filtres et recherche globale se combinent dans une seule requête.
/// Les noms de colonnes sont des noms logiques, traduits en SQL
/// par les listes blanches du repository.
/// </summary>
public sealed record PageRequest
{
    /// <summary>Nombre de lignes à ignorer.</summary>
    public int Skip { get; init; }

    /// <summary>Nombre de lignes à renvoyer.</summary>
    public int Take { get; init; } = 20;

    /// <summary>Tris appliqués dans l'ordre. Vide : tri par défaut du repository.</summary>
    public IReadOnlyList<SortCriteria> Sorts { get; init; } = [];

    /// <summary>Filtres par colonne, combinés par ET.</summary>
    public IReadOnlyList<FilterCriteria> Filters { get; init; } = [];

    /// <summary>Terme cherché dans les colonnes de recherche du repository.</summary>
    public string? Search { get; init; }

    /// <summary>Crée une demande à partir d'un numéro de page (à partir de 1).</summary>
    public static PageRequest ForPage(int pageNumber, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        return new PageRequest
        {
            Skip = checked((pageNumber - 1) * pageSize),
            Take = pageSize
        };
    }
}

/// <summary>
/// Tri sur une colonne.
/// </summary>
public sealed record SortCriteria(
    string Column,
    bool Descending = false
);
