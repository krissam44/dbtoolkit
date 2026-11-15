namespace SqliteToolkit.Configuration;

public sealed class SqliteOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public int DefaultTimeout { get; set; } = 30;

    public bool ForeignKeys { get; set; } = true;
}