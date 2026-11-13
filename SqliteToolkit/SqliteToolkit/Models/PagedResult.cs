
namespace SqliteToolkit.Models;

public sealed class PagedResult<TEntity>
{
    public IReadOnlyList<TEntity> Items { get; init; }
        = Array.Empty<TEntity>();

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages =>
        PageSize > 0
            ? (int)Math.Ceiling((double)TotalCount / PageSize)
            : 0;

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
