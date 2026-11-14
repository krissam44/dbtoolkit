namespace DbToolkit.Models;

/// <summary>
/// Opérateurs disponibles pour les filtres.
/// </summary>
public enum FilterOperator
{
    Contains,
    Equals,
    StartsWith,
    EndsWith,
    NotContains,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsEmpty,
    IsNotEmpty
}

/// <summary>
/// Représente un critère de filtrage indépendant de l'interface.
/// La valeur peut être un texte, un nombre, un booléen ou une date ;
/// elle est ignorée pour <see cref="FilterOperator.IsEmpty"/>
/// et <see cref="FilterOperator.IsNotEmpty"/>.
/// </summary>
public sealed record FilterCriteria(
    string Column,
    FilterOperator Operator,
    object? Value = null
);
