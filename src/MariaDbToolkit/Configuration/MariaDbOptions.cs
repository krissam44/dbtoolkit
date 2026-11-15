namespace MariaDbToolkit.Configuration;

public sealed class MariaDbOptions
{
    /// <summary>
    /// Chaîne de connexion MySqlConnector, par exemple
    /// <c>Server=localhost;Port=3306;Database=erp;User ID=erp;Password=...</c>.
    /// À fournir par la configuration (user-secrets, variable d'environnement), jamais dans le code.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Délai maximal d'une commande, en secondes.</summary>
    public int DefaultCommandTimeout { get; set; } = 30;
}
