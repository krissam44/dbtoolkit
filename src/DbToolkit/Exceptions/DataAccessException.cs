namespace DbToolkit.Exceptions;

/// <summary>
/// Nature d'une erreur d'accès aux données, pour que l'appelant
/// (une API, par exemple) puisse réagir sans lire le message du moteur.
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

    /// <summary>Base ou ligne verrouillée par une autre connexion : l'opération peut être retentée.</summary>
    Busy,

    /// <summary>
    /// Interblocage : le moteur a annulé cette transaction pour en laisser passer une autre.
    /// L'opération peut être retentée en entier.
    /// </summary>
    Deadlock,

    /// <summary>
    /// La ligne a été modifiée par quelqu'un d'autre depuis sa lecture
    /// (voir <see cref="ConcurrencyException"/>).
    /// </summary>
    ConcurrencyConflict
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

    public DataAccessException(string message, DataAccessErrorKind kind)
        : base(message)
    {
        Kind = kind;
    }

    /// <summary>Nature de l'erreur ; <see cref="DataAccessErrorKind.Unknown"/> si elle n'est pas reconnue.</summary>
    public DataAccessErrorKind Kind { get; }

    /// <summary>
    /// Vrai si l'erreur est passagère (verrou, interblocage) : la même opération,
    /// relancée, a toutes les chances de réussir.
    /// </summary>
    public bool IsTransient => Kind is DataAccessErrorKind.Busy or DataAccessErrorKind.Deadlock;
}

/// <summary>
/// Une modification ou une suppression gardée par un numéro de version n'a touché
/// aucune ligne alors que la ligne existe encore : quelqu'un d'autre l'a modifiée
/// depuis sa lecture. Il faut la relire avant de recommencer.
/// </summary>
public class ConcurrencyException : DataAccessException
{
    public ConcurrencyException(string message)
        : base(message, DataAccessErrorKind.ConcurrencyConflict)
    {
    }
}
