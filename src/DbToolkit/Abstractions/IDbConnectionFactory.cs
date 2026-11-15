using System.Data.Common;

namespace DbToolkit.Abstractions;

/// <summary>
/// Point d'entrée d'un fournisseur (SQLite, MariaDB...) : il crée les
/// connexions et décrit les particularités de son moteur.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Particularités du moteur de ce fournisseur.</summary>
    ISqlDialect Dialect { get; }

    /// <summary>Crée une connexion fermée, à ouvrir puis libérer par l'appelant.</summary>
    DbConnection CreateConnection();
}
