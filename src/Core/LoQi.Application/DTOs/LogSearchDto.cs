namespace LoQi.Application.DTOs;


public class LogSearchDto
{
    public string? UniqueId { get; set; } 
    public string? SearchText { get; set; }
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public int? LevelId { get; set; }
    public string? Source { get; set; }
    public string? CorrelationId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string OrderBy { get; set; } = "timestamp"; // timestamp, level, source
    public bool Descending { get; set; } = true;
}

