using Dapper;
using SqliteToolkit.Abstractions;
using SqliteToolkit.Demo.Repositories;
using SqliteToolkit.Exceptions;

namespace SqliteToolkit.Demo.Data;

public class DatabaseConnectionTestService
{
    private readonly IDbConnectionFactory _factory;

    private readonly ErrorTestRepository _errorTestRepository;

    private readonly ClientRepository _clientRepository;

    public DatabaseConnectionTestService(
        IDbConnectionFactory factory,
        ErrorTestRepository errorTestRepository,
        ClientRepository clientRepository)
    {
        _factory = factory;
        _errorTestRepository = errorTestRepository;
        _clientRepository = clientRepository;
    }

    public async Task<string> TestConnectionAsync()
    {
        await using var db = _factory.CreateConnection();

        await db.OpenAsync();

        var result = await db.ExecuteScalarAsync<string>(
            "SELECT sqlite_version();");

        return $"Connexion SQLite réussie. Version : {result}";
    }

    public async Task<string> TestRepositoryErrorAsync()
    {
        try
        {
            await _errorTestRepository.GetByIdAsync(1);

            return "ERREUR : aucune exception n'a été déclenchée.";
        }
        catch (DataAccessException ex)
        {
            return $"Test réussi : DataAccessException interceptée. " +
                   $"Exception interne : {ex.InnerException?.GetType().Name}";
        }
    }

    public async Task<string> TestViolationContrainteAsync()
    {
        try
        {
            await _errorTestRepository.InsertAsync(
                new TestEntity { Nom = null });

            return "ERREUR : l'insertion aurait dû être refusée.";
        }
        catch (DataAccessException ex)
        {
            return $"Test réussi : DataAccessException interceptée. " +
                   $"Exception interne : {ex.InnerException?.GetType().Name}";
        }
    }

    public async Task<string> TestCancellationAsync()
    {
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        try
        {
            await _errorTestRepository.GetByIdAsync(
                1,
                cancellationToken: cts.Token);

            return "ERREUR : l'opération aurait dû être annulée.";
        }
        catch (OperationCanceledException)
        {
            return "Test réussi : OperationCanceledException conservée.";
        }
        catch (DataAccessException)
        {
            return "ERREUR : l'annulation a été transformée en DataAccessException.";
        }
    }

}