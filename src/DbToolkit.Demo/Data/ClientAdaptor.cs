using System.Text.Json;
using DbToolkit.Adapters.Syncfusion;
using DbToolkit.Demo.Models;
using DbToolkit.Demo.Services;
using Syncfusion.Blazor;

namespace DbToolkit.Demo.Data;

/// <summary>
/// Adaptateur personnalisé de la grille Syncfusion : chaque demande de la
/// grille (page, tris, filtres, recherche) est traduite en PageRequest
/// et exécutée en SQL.
/// </summary>
public class ClientAdaptor : DataAdaptor
{
    private readonly ClientService _clients;

    public ClientAdaptor(ClientService clients)
    {
        _clients = clients;
    }

    public override async Task<object> ReadAsync(
        DataManagerRequest dataManagerRequest,
        string? key = null)
    {
        var page = await _clients.GetPagedAsync(dataManagerRequest.ToPageRequest());

        return page.ToDataResult(dataManagerRequest);
    }

    public override async Task<object> InsertAsync(
        DataManager dataManager,
        object value,
        string? key)
    {
        var client = (Client)value;

        client.Id = await _clients.InsertAsync(client);

        return client;
    }

    public override async Task<object> UpdateAsync(
        DataManager dataManager,
        object value,
        string? keyField,
        string? key)
    {
        await _clients.UpdateAsync((Client)value);

        return value;
    }

    public override async Task<object> RemoveAsync(
        DataManager dataManager,
        object value,
        string? keyField,
        string? key)
    {
        // La grille transmet la valeur de la clé primaire.
        var id = value is JsonElement json
            ? json.GetInt32()
            : Convert.ToInt32(value);

        await _clients.DeleteAsync(id);

        return value;
    }
}
