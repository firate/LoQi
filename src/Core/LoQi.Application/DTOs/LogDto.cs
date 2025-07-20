namespace LoQi.Application.DTOs;

public record LogDto
{
    public string? UniqueId { get; set; } 
    public string? CorrelationId { get; set; }
    public string Message { get; set; }
    public string Source { get; set; }
    public int LevelId { get; set; }
    public DateTimeOffset Date { get; set; }
}