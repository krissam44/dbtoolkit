namespace SqliteToolkit.Exceptions;

/// <summary>
/// Nature d'une erreur d'accès aux données, pour que l'appelant
/// (une API, par exemple) puisse réagir sans lire le message SQLite.
/// </summary>
public enum DataAccessErrorKind
{
    Unknown,

    /// <summary>Doublon : contrainte UNIQUE ou clé primaire.</summary>
    UniqueViolation,

    /// <summary>Clé étrangère : la ligne référencée n'existe pas, ou est encore utilisée.</summary>
    ForeignKeyViolation,

    /// <summary>Valeur obligatoire manquante (NOT NULL).</summary>
    NotNullViolation,

    /// <summary>Contrainte CHECK non respectée.</summary>
    CheckViolation,

    /// <summary>Base verrouillée par une autre connexion : l'opération peut être retentée.</summary>
    Busy
}

public class DataAccessException : Exception
{
    public DataAccessException(string message)
        : base(message)
    {
    }

    public DataAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DataAccessException(
        string message,
        Exception innerException,
        DataAccessErrorKind kind)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>Nature de l'erreur ; <see cref="DataAccessErrorKind.Unknown"/> si elle n'est pas reconnue.</summary>
    public DataAccessErrorKind Kind { get; }
}
