namespace SqliteToolkit.Migrations;

public interface IMigration
{
    int Version { get; }

    string Name { get; }

    string Sql { get; }
}