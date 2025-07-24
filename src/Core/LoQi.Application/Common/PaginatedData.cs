namespace LoQi.Application.Common;

public record PaginatedData<T>
{
    public List<T> Items { get; set; } = [];

    public PaginationInfo PaginationInfo { get; set; }
    
    public PaginatedData(List<T> items, PaginationInfo pagination)
    {
        Items = items;
        PaginationInfo = pagination;
    }

    public static PaginatedData<T> Create(List<T> items, int page, int pageSize, int totalCount)
        => new(items, new PaginationInfo(page, pageSize, totalCount));

    public static PaginatedData<T> Create(List<T> items, PaginationInfo paginationInfo)
        => new(items, new PaginationInfo(paginationInfo.Page, paginationInfo.PageSize, paginationInfo.TotalCount));
    
    // Empty pagination
    public static PaginatedData<T> Empty(int page = 1, int pageSize = 10)
        => new([], new PaginationInfo(page, pageSize,0));
}