using Microsoft.Data.Sqlite;

namespace SqliteToolkit.Abstractions;

public interface IDbConnectionFactory
{
    SqliteConnection CreateConnection();
}