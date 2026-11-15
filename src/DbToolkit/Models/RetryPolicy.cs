namespace DbToolkit.Models;

/// <summary>
/// Reprise automatique après une erreur passagère (attente de verrou expirée,
/// interblocage). Chaque nouvel essai attend deux fois plus longtemps que le
/// précédent, avec une part de hasard pour que deux transactions qui se sont
/// bloquées ne repartent pas au même instant.
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>Trois reprises, la première après 50 ms environ.</summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>Aucune reprise : l'erreur passagère remonte immédiatement.</summary>
    public static RetryPolicy None { get; } = new() { MaxRetries = 0 };

    /// <summary>Nombre de reprises après le premier essai.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Attente avant la première reprise.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Attente avant la reprise numéro <paramref name="retry"/> (à partir de 1).</summary>
    public TimeSpan DelayFor(int retry)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retry, 1);

        var milliseconds = BaseDelay.TotalMilliseconds * Math.Pow(2, retry - 1);

        // Entre 100 % et 150 % de l'attente nominale.
        return TimeSpan.FromMilliseconds(milliseconds * (1 + Random.Shared.NextDouble() / 2));
    }
}
