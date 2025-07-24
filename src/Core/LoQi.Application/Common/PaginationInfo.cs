namespace LoQi.Application.Common;

public record PaginationInfo
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    public bool IsFirstPage => Page == 1;
    public bool IsLastPage => Page == TotalPages;
    public int StartIndex => Math.Max(1, (Page - 1) * PageSize + 1);
    public int EndIndex => Math.Min(Page * PageSize, TotalCount);

    // Constructor
    public PaginationInfo(int page, int pageSize, int totalCount)
    {
        Page = Math.Max(1, page);
        PageSize = Math.Max(1, pageSize);
        TotalCount = Math.Max(0, totalCount);
    }

    // Static factory method
    public static PaginationInfo Create(int page, int pageSize, int totalCount)
        => new(page, pageSize, totalCount);

    // Empty pagination
    public static PaginationInfo Empty(int page = 1, int pageSize = 10)
        => new(page, pageSize, 0);
}