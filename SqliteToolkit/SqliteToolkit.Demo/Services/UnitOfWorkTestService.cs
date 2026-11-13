using SqliteToolkit.Abstractions;
using SqliteToolkit.Demo.Models;
using SqliteToolkit.Demo.Repositories;

namespace SqliteToolkit.Demo.Services;

public class UnitOfWorkTestService
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ClientRepository _clients;

    public UnitOfWorkTestService(
        IUnitOfWorkFactory unitOfWorkFactory,
        ClientRepository clients)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _clients = clients;
    }

    public async Task<string> TestCommitAsync()
    {
        try
        {
            await using var unitOfWork = await _unitOfWorkFactory.BeginAsync();

            await _clients.InsertAsync(
                new Client
                {
                    Nom = "Test UoW 1",
                    Prenom = "Commit"
                },
                unitOfWork);

            await _clients.InsertAsync(
                new Client
                {
                    Nom = "Test UoW 2",
                    Prenom = "Commit"
                },
                unitOfWork);

            await unitOfWork.CommitAsync();

            return "Test réussi : les deux clients ont été enregistrés.";
        }
        catch (Exception ex)
        {
            return $"Échec du test Commit : {ex.Message}";
        }
    }


    public async Task<string> TestRollbackAsync()
    {
        try
        {
            await using var unitOfWork = await _unitOfWorkFactory.BeginAsync();

            await _clients.InsertAsync(
                new Client
                {
                    Nom = "Test UoW 1",
                    Prenom = "Rollback"
                },
                unitOfWork);

            await _clients.InsertAsync(
                new Client
                {
                    Nom = "Test UoW 2",
                    Prenom = "Rollback"
                },
                unitOfWork);

            await unitOfWork.RollbackAsync();

            return "Test réussi : la transaction a été annulée.";
        }
        catch (Exception ex)
        {
            return $"Échec du test Rollback : {ex.Message}";
        }
    }

}
