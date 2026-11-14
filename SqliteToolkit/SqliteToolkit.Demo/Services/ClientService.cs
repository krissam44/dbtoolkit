
using SqliteToolkit.Demo.Models;
using SqliteToolkit.Demo.Repositories;
using DbToolkit.Models;

namespace SqliteToolkit.Demo.Services;

public class ClientService
{
    private readonly ClientRepository _repository;

    public ClientService(ClientRepository repository)
    {
        _repository = repository;
    }

    // ---------------------------------------------------------------------------------
    public Task<Client?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(
            id,
            cancellationToken: cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<IReadOnlyList<Client>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(
            cancellationToken: cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<PagedResult<Client>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetPagedAsync(
            request,
            cancellationToken: cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<int> InsertAsync(
        Client client,
        CancellationToken cancellationToken = default)
    {
        return _repository.InsertAsync(
            client,
            cancellationToken: cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<bool> UpdateAsync(
        Client client,
        CancellationToken cancellationToken = default)
    {
        return _repository.UpdateAsync(
            client,
            cancellationToken: cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    public Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return _repository.DeleteAsync(
            id,
            cancellationToken: cancellationToken);
    }
}
