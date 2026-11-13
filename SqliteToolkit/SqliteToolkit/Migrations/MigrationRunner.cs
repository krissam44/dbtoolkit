
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SqliteToolkit.Abstractions;
using System.Reflection;

namespace SqliteToolkit.Migrations;

public sealed class MigrationRunner
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(
        IDbConnectionFactory factory,
        ILogger<MigrationRunner> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task MigrateAsync(
        Assembly assembly,
        string resourcePrefix,
        CancellationToken cancellationToken = default)
    {
        await using var db = _factory.CreateConnection();

        await db.OpenAsync(cancellationToken);

        // Création de la table de suivi
        await db.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS SchemaMigrations
            (
                Version INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                AppliedAt TEXT NOT NULL
            );
            """,
            cancellationToken: cancellationToken));

        // Récupération des migrations déjà exécutées
        var applied = (await db.QueryAsync<int>(
            new CommandDefinition(
                "SELECT Version FROM SchemaMigrations",
                cancellationToken: cancellationToken)))
            .ToHashSet();

        // Découverte des scripts SQL embarqués
        var prefix = resourcePrefix.TrimEnd('.') + ".";

        var resources = assembly.GetManifestResourceNames()
            .Where(name =>
                name.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(
                    ".sql",
                    StringComparison.OrdinalIgnoreCase))
            .Select(resource =>
            {
                var scriptName = resource[prefix.Length..];

                var match = System.Text.RegularExpressions.Regex.Match(
                    scriptName,
                    @"^(\d+)_(.+)\.sql$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    throw new InvalidOperationException(
                        $"Nom de migration invalide : {resource}");
                }

                return new
                {
                    Resource = resource,
                    Version = int.Parse(match.Groups[1].Value),
                    Name = match.Groups[2].Value
                };
            })
            .OrderBy(m => m.Version)
            .ToList();

        var duplicateVersions = resources
            .GroupBy(m => m.Version)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateVersions.Count > 0)
        {
            throw new InvalidOperationException(
                $"Versions de migrations dupliquées : " +
                $"{string.Join(", ", duplicateVersions)}");
        }

        foreach (var migration in resources)
        {
            var resource = migration.Resource;
            var version = migration.Version;
            var name = migration.Name;
            cancellationToken.ThrowIfCancellationRequested();

            if (applied.Contains(version))
                continue;

            await using var stream =
                assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException(
                    $"Ressource introuvable : {resource}");

            using var reader = new StreamReader(stream);

            var sql = await reader.ReadToEndAsync(cancellationToken);

            _logger.LogInformation(
                "Application de la migration {Version}: {Name}",
                version,
                name);

            // Une migration = une transaction
            await using var transaction =
                await db.BeginTransactionAsync(cancellationToken);

            try
            {
                await db.ExecuteAsync(new CommandDefinition(
                    sql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));

                await db.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO SchemaMigrations
                        (Version, Name, AppliedAt)
                    VALUES
                        (@Version, @Name, @AppliedAt);
                    """,
                    new
                    {
                        Version = version,
                        Name = name,
                        AppliedAt = DateTime.UtcNow.ToString("O")
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Migration {Version} appliquée.",
                    version);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
