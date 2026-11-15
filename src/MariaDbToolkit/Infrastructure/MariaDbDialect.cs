using System.Data.Common;
using DbToolkit.Abstractions;
using DbToolkit.Exceptions;
using MySqlConnector;

namespace MariaDbToolkit.Infrastructure;

public sealed class MariaDbDialect : ISqlDialect
{
    public static MariaDbDialect Instance { get; } = new();

    public string Name => "MariaDB";

    // Codes d'erreur : https://mariadb.com/kb/en/mariadb-error-codes/
    public DataAccessErrorKind Classify(DbException exception)
    {
        if (exception is not MySqlException mariaDb)
            return DataAccessErrorKind.Unknown;

        return mariaDb.Number switch
        {
            1062 or 1586 => DataAccessErrorKind.UniqueViolation,        // doublon sur clé unique ou primaire
            1451 or 1452 or 1216 or 1217 => DataAccessErrorKind.ForeignKeyViolation,
            1048 or 1364 => DataAccessErrorKind.NotNullViolation,       // colonne NULL interdite, valeur par défaut absente
            4025 or 3819 => DataAccessErrorKind.CheckViolation,         // CHECK (MariaDB, MySQL)
            1205 => DataAccessErrorKind.Busy,                           // attente de verrou expirée
            1213 => DataAccessErrorKind.Deadlock,                       // interblocage : transaction annulée
            _ => DataAccessErrorKind.Unknown
        };
    }
}
