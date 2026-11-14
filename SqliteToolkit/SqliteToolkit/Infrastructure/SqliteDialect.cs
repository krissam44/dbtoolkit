using System.Data.Common;
using DbToolkit.Abstractions;
using DbToolkit.Exceptions;
using Microsoft.Data.Sqlite;

namespace SqliteToolkit.Infrastructure;

public sealed class SqliteDialect : ISqlDialect
{
    public static SqliteDialect Instance { get; } = new();

    public string Name => "SQLite";

    // Codes étendus SQLite : https://www.sqlite.org/rescode.html
    public DataAccessErrorKind Classify(DbException exception)
    {
        if (exception is not SqliteException sqlite)
            return DataAccessErrorKind.Unknown;

        return sqlite.SqliteExtendedErrorCode switch
        {
            2067 or 1555 => DataAccessErrorKind.UniqueViolation,   // UNIQUE, PRIMARY KEY
            787 => DataAccessErrorKind.ForeignKeyViolation,
            1299 => DataAccessErrorKind.NotNullViolation,
            275 => DataAccessErrorKind.CheckViolation,
            _ => sqlite.SqliteErrorCode is 5 or 6                   // BUSY, LOCKED
                ? DataAccessErrorKind.Busy
                : DataAccessErrorKind.Unknown
        };
    }
}
