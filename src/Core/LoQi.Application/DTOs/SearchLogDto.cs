namespace LoQi.Application.DTOs;


public class LogSearchDto
{
    public string? UniqueId { get; set; } 
    public string? SearchText { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int? LevelId { get; set; }
    public string? Source { get; set; }
    public Guid? CorrelationId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string OrderBy { get; set; } = "timestamp"; // timestamp, level, source
    public bool Descending { get; set; } = true;
}

// Search result DTO
public class LogSearchResult
{
    public List<LogDto> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}