using System.Data.Common;
using DbToolkit.Exceptions;

namespace DbToolkit.Abstractions;

/// <summary>
/// Ce qui diffère d'un moteur de base de données à l'autre.
/// Le SQL généré par la pagination est volontairement commun
/// (paramètres nommés @x, LIMIT / OFFSET, LIKE ... ESCAPE).
/// </summary>
public interface ISqlDialect
{
    /// <summary>Nom du moteur, pour les messages et les journaux.</summary>
    string Name { get; }

    /// <summary>
    /// Traduit une erreur du moteur en nature d'erreur indépendante
    /// (doublon, clé étrangère, base occupée...).
    /// </summary>
    DataAccessErrorKind Classify(DbException exception);
}
