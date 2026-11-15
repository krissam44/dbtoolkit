namespace DbToolkit.Migrations;

public interface IMigration
{
    int Version { get; }

    string Name { get; }

    string Sql { get; }
}