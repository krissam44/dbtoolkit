namespace DbToolkit.Models;

/// <summary>
/// Bornes à appliquer à une <see cref="PageRequest"/> venue de l'extérieur
/// (API publique, composant d'interface) pour qu'un appelant ne puisse pas
/// réclamer toute la table ni empiler des centaines de filtres.
/// </summary>
public sealed record PageRequestLimits
{
    public static PageRequestLimits Default { get; } = new();

    /// <summary>Nombre maximal de lignes par page.</summary>
    public int MaxPageSize { get; init; } = 200;

    /// <summary>Longueur maximale du terme de recherche.</summary>
    public int MaxSearchLength { get; init; } = 100;

    /// <summary>Nombre maximal de filtres.</summary>
    public int MaxFilters { get; init; } = 10;

    /// <summary>Nombre maximal de tris.</summary>
    public int MaxSorts { get; init; } = 5;

    /// <summary>
    /// Renvoie la demande si elle respecte les bornes.
    /// </summary>
    /// <exception cref="ArgumentException">Une borne est dépassée ; le message est lisible par l'appelant final.</exception>
    public PageRequest Validate(PageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Take > MaxPageSize)
        {
            throw new ArgumentException(
                $"La taille de page ne peut pas dépasser {MaxPageSize}.");
        }

        if (request.Search is { Length: > 0 } search && search.Length > MaxSearchLength)
        {
            throw new ArgumentException(
                $"Le terme de recherche ne peut pas dépasser {MaxSearchLength} caractères.");
        }

        if (request.Filters.Count > MaxFilters)
        {
            throw new ArgumentException(
                $"Le nombre de filtres ne peut pas dépasser {MaxFilters}.");
        }

        if (request.Sorts.Count > MaxSorts)
        {
            throw new ArgumentException(
                $"Le nombre de tris ne peut pas dépasser {MaxSorts}.");
        }

        return request;
    }
}
