
namespace SqliteToolkit.Demo.Models;

public class Client
{
    public int Id { get; set; }

    public string Nom { get; set; } = string.Empty;

    public string? Prenom { get; set; }

    public string? Email { get; set; }

    public string? Telephone { get; set; }
}
