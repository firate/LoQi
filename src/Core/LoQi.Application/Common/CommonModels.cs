namespace LoQi.Application.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    public bool IsFirstPage => Page == 1;
    public bool IsLastPage => Page == TotalPages;
    public int StartIndex => (Page - 1) * PageSize + 1;
    public int EndIndex => Math.Min(Page * PageSize, TotalCount);

    // Constructors
    public PagedResult()
    {
    }

    public PagedResult(List<T> items, int totalCount, int page, int pageSize)
    {
        Items = items ?? new List<T>();
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    // Static factory methods
    public static PagedResult<T> Create(List<T> items, int totalCount, int page, int pageSize)
    {
        return new PagedResult<T>(items, totalCount, page, pageSize);
    }

    public static PagedResult<T> Empty(int page = 1, int pageSize = 10)
    {
        return new PagedResult<T>(new List<T>(), 0, page, pageSize);
    }

    // Extension method için mapping desteği
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        var mappedItems = Items.Select(mapper).ToList();
        return new PagedResult<TResult>(mappedItems, TotalCount, Page, PageSize);
    }

    // Pagination info as separate object (isteğe bağlı)
    public PaginationInfo GetPaginationInfo()
    {
        return new PaginationInfo
        {
            Page = Page,
            PageSize = PageSize,
            TotalCount = TotalCount,
            TotalPages = TotalPages,
            HasNextPage = HasNextPage,
            HasPreviousPage = HasPreviousPage,
            StartIndex = StartIndex,
            EndIndex = EndIndex
        };
    }
}

public class PaginationInfo
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
}


