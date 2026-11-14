using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using SqliteToolkit.Exceptions;

namespace SqliteToolkit.Adapters.AspNetCore;

/// <summary>
/// Traduit les exceptions de SqliteToolkit en réponses HTTP (ProblemDetails)
/// sans jamais exposer le SQL ni le détail interne de SQLite.
/// </summary>
public static class SqliteToolkitProblems
{
    /// <summary>
    /// <list type="bullet">
    /// <item><see cref="ArgumentException"/> (colonne ou opérateur interdit, borne dépassée) : 400.</item>
    /// <item><see cref="DataAccessException"/> : 409 pour un doublon ou une clé étrangère,
    /// 400 pour une valeur obligatoire manquante ou une règle CHECK, 503 si la base est occupée, sinon 500.</item>
    /// </list>
    /// Toute autre exception n'est pas prise en charge (<c>false</c>).
    /// </summary>
    public static bool TryCreate(
        Exception exception,
        [NotNullWhen(true)] out IResult? problem)
    {
        switch (exception)
        {
            case ArgumentNullException:
                // Erreur de programmation, pas une erreur du client.
                break;

            case ArgumentException argument:
                problem = TypedResults.Problem(
                    title: "Requête invalide",
                    detail: argument.Message,
                    statusCode: StatusCodes.Status400BadRequest);
                return true;

            case DataAccessException dataAccess:
                problem = FromDataAccess(dataAccess);
                return true;
        }

        problem = null;
        return false;
    }

    private static IResult FromDataAccess(DataAccessException exception)
    {
        var (status, title, detail) = exception.Kind switch
        {
            DataAccessErrorKind.UniqueViolation => (
                StatusCodes.Status409Conflict,
                "Doublon",
                "Cette valeur existe déjà."),

            DataAccessErrorKind.ForeignKeyViolation => (
                StatusCodes.Status409Conflict,
                "Relation invalide",
                "L'opération viole une relation avec une autre donnée."),

            DataAccessErrorKind.NotNullViolation => (
                StatusCodes.Status400BadRequest,
                "Valeur manquante",
                "Une valeur obligatoire est manquante."),

            DataAccessErrorKind.CheckViolation => (
                StatusCodes.Status400BadRequest,
                "Valeur refusée",
                "Une valeur ne respecte pas les règles de la base."),

            DataAccessErrorKind.Busy => (
                StatusCodes.Status503ServiceUnavailable,
                "Base occupée",
                "La base est occupée, réessayez dans un instant."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Erreur d'accès aux données",
                "Une erreur est survenue lors de l'accès aux données.")
        };

        return TypedResults.Problem(
            title: title,
            detail: detail,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["kind"] = exception.Kind.ToString()
            });
    }
}
