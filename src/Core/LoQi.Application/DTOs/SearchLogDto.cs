namespace LoQi.Application.DTOs;

public record SearchLogDto
{
    public string? UniqueId { get; set; } 
    public string? CorrelationId { get; set; }
    public string? SearchTerm { get; set; }
    public DateTimeOffset BeginDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public string Source { get; set; }
    public int LevelId { get; set; }
    
}