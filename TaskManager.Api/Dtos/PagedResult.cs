namespace TaskManager.Api.Dtos
{
    /// <summary>
    /// Generic wrapper for paginated responses.
    /// </summary>
    public sealed class PagedResult<T>
    {
        public required IReadOnlyList<T> Items { get; init; }
        public required int Total { get; init; }
        public required int Page { get; init; }
        public required int PageSize { get; init; }
    }
}
